using System.Security.Claims;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/messages")]
    public class MessagingController : ControllerBase
    {
        private readonly AppDbContext          _db;
        private readonly UserManager<AppUser>  _users;
        private readonly IHubContext<MessageHub> _hub;

        public MessagingController(AppDbContext db, UserManager<AppUser> users, IHubContext<MessageHub> hub)
        {
            _db    = db;
            _users = users;
            _hub   = hub;
        }

        // ── Contacts ─────────────────────────────────────────────────────────────

        // GET /api/messages/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var myId = GetUserId();
            var me = await _users.FindByIdAsync(myId);
            if (me == null) return Unauthorized();

            IQueryable<AppUser> query = _users.Users.Where(u => u.Id != myId);

            if (string.Equals(me.SfRole, "coach", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u =>
                    u.SfRole == "athlete" &&
                    !string.IsNullOrEmpty(me.Organisation) &&
                    u.Organisation == me.Organisation);
            }

            var users = await query
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.SfRole, u.Organisation })
                .ToListAsync();

            var unreadBySender = await _db.Messages
                .Where(m => m.ChannelId == null && m.ReceiverId == myId && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SenderId, x => x.Count);

            var payload = users.Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.SfRole,
                u.Organisation,
                unreadCount = unreadBySender.TryGetValue(u.Id, out var count) ? count : 0,
            });

            return Ok(payload);
        }

        // GET /api/messages/chats — Get all chat conversations with last message
        [HttpGet("chats")]
        public async Task<IActionResult> GetChats()
        {
            var myId = GetUserId();
            
            // Get all unique senders/receivers this athlete has communicated with
            var senderIds = await _db.Messages
                .Where(m => m.ReceiverId == myId)
                .Select(m => m.SenderId)
                .Distinct()
                .ToListAsync();

            var receiverIds = await _db.Messages
                .Where(m => m.SenderId == myId)
                .Select(m => m.ReceiverId)
                .Distinct()
                .ToListAsync();

            var contactIds = senderIds.Union(receiverIds).Where(id => !string.IsNullOrEmpty(id)).ToList();

            var chats = new List<object>();

            foreach (var contactId in contactIds)
            {
                var lastMsg = await _db.Messages
                    .Where(m => m.ChannelId == null &&
                                ((m.SenderId == myId && m.ReceiverId == contactId) ||
                                 (m.SenderId == contactId && m.ReceiverId == myId)))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                if (lastMsg == null) continue;

                var contact = await _users.FindByIdAsync(contactId);
                var unreadCount = await _db.Messages
                    .CountAsync(m => m.SenderId == contactId && m.ReceiverId == myId && !m.IsRead);

                chats.Add(new
                {
                    id = contactId,
                    name = contact != null ? $"{contact.FirstName} {contact.LastName}".Trim() : "Unknown",
                    lastMessage = lastMsg.Content,
                    lastMessageTime = FormatTime(lastMsg.SentAt),
                    unread = unreadCount,
                    senderName = lastMsg.SenderName,
                });
            }

            return Ok(chats.OrderByDescending(c => ((dynamic)c).lastMessageTime));
        }

        // ── Direct Messages ───────────────────────────────────────────────────────

        // GET /api/messages/direct/{otherId}?page=1
        [HttpGet("direct/{otherId}")]
        public async Task<IActionResult> GetDirectHistory(string otherId, [FromQuery] int page = 1)
        {
            var myId = GetUserId();
            if (!await CanMessageDirectly(myId, otherId)) return Forbid();

            var msgs = await _db.Messages
                .Where(m => m.ChannelId == null &&
                            ((m.SenderId == myId   && m.ReceiverId == otherId) ||
                             (m.SenderId == otherId && m.ReceiverId == myId)))
                .OrderBy(m => m.SentAt)
                .Skip((page - 1) * 50)
                .Take(50)
                .ToListAsync();
            return Ok(msgs.Select(ToDto));
        }

        // POST /api/messages/direct  — REST alternative to hub (useful for mobile)
        [HttpPost("direct")]
        public async Task<IActionResult> SendDirect([FromBody] SendDirectMessageRequest req)
        {
            var senderId   = GetUserId();
            var senderName = GetSenderName();
            if (!await CanMessageDirectly(senderId, req.ReceiverId)) return Forbid();

            var msg = new Message
            {
                SenderId   = senderId,
                SenderName = senderName,
                ReceiverId = req.ReceiverId,
                Content    = req.Content,
                Type       = req.Type,
                Metadata   = req.Metadata,
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var dto = ToDto(msg);
            await _hub.Clients.Group($"user-{senderId}").SendAsync("NewMessage", dto);
            await _hub.Clients.Group($"user-{req.ReceiverId}").SendAsync("NewMessage", dto);
            return Ok(dto);
        }

        // PATCH /api/messages/direct/{otherId}/read — mark all incoming as read
        [HttpPatch("direct/{otherId}/read")]
        public async Task<IActionResult> MarkDirectRead(string otherId)
        {
            var myId   = GetUserId();
            if (!await CanMessageDirectly(myId, otherId)) return Forbid();

            var unread = await _db.Messages
                .Where(m => m.SenderId == otherId && m.ReceiverId == myId && !m.IsRead)
                .ToListAsync();
            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ── Channels ─────────────────────────────────────────────────────────────

        // GET /api/messages/channels
        [HttpGet("channels")]
        public async Task<IActionResult> GetChannels()
        {
            var myId     = GetUserId();
            var channels = await _db.ChannelMembers
                .Where(cm => cm.UserId == myId)
                .Include(cm => cm.Channel)
                .Select(cm => new
                {
                    cm.Channel!.Id,
                    cm.Channel!.Name,
                    cm.Channel!.Type,
                    cm.Channel!.CreatedAt,
                })
                .ToListAsync();
            return Ok(channels);
        }

        // POST /api/messages/channels
        [HttpPost("channels")]
        public async Task<IActionResult> CreateChannel([FromBody] CreateChannelRequest req)
        {
            var myId   = GetUserId();
            var channel = new Channel
            {
                Id          = Guid.NewGuid().ToString(),
                Name        = req.Name,
                Type        = req.Type,
                CreatedById = myId,
            };
            _db.Channels.Add(channel);

            var memberIds = req.MemberIds.Distinct().ToList();
            if (!memberIds.Contains(myId)) memberIds.Add(myId);

            foreach (var uid in memberIds)
                _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = uid });

            await _db.SaveChangesAsync();
            return Ok(new { channel.Id, channel.Name, channel.Type });
        }

        // GET /api/messages/channels/{channelId}?page=1
        [HttpGet("channels/{channelId}")]
        public async Task<IActionResult> GetChannelHistory(string channelId, [FromQuery] int page = 1)
        {
            var myId     = GetUserId();
            var isMember = await _db.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == myId);
            if (!isMember) return Forbid();

            var msgs = await _db.Messages
                .Where(m => m.ChannelId == channelId)
                .OrderBy(m => m.SentAt)
                .Skip((page - 1) * 50)
                .Take(50)
                .ToListAsync();
            return Ok(msgs.Select(ToDto));
        }

        // POST /api/messages/channels/{channelId}
        [HttpPost("channels/{channelId}")]
        public async Task<IActionResult> SendToChannel(string channelId, [FromBody] SendChannelMessageRequest req)
        {
            var senderId = GetUserId();
            var isMember = await _db.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == senderId);
            if (!isMember) return Forbid();

            var msg = new Message
            {
                SenderId   = senderId,
                SenderName = GetSenderName(),
                ChannelId  = channelId,
                Content    = req.Content,
                Type       = req.Type,
                Metadata   = req.Metadata,
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var dto = ToDto(msg);
            await _hub.Clients.Group($"channel-{channelId}").SendAsync("NewMessage", dto);
            return Ok(dto);
        }

        // ── Badge count ───────────────────────────────────────────────────────────

        // GET /api/messages/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var myId  = GetUserId();
            var count = await _db.Messages
                .CountAsync(m => m.ReceiverId == myId && !m.IsRead);
            return Ok(new { count });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();

        private string GetSenderName()
        {
            var given  = User.FindFirst(ClaimTypes.GivenName)?.Value  ?? "";
            var family = User.FindFirst(ClaimTypes.Surname)?.Value    ?? "";
            return $"{given} {family}".Trim();
        }

        private async Task<bool> CanMessageDirectly(string senderId, string receiverId)
        {
            var sender = await _users.FindByIdAsync(senderId);
            var receiver = await _users.FindByIdAsync(receiverId);
            if (sender == null || receiver == null) return false;

            if (string.Equals(sender.SfRole, "coach", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(receiver.SfRole, "athlete", StringComparison.OrdinalIgnoreCase)
                       && !string.IsNullOrWhiteSpace(sender.Organisation)
                       && string.Equals(sender.Organisation, receiver.Organisation, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(receiver.SfRole, "coach", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(sender.SfRole, "athlete", StringComparison.OrdinalIgnoreCase)
                       && !string.IsNullOrWhiteSpace(receiver.Organisation)
                       && string.Equals(sender.Organisation, receiver.Organisation, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static MessageDto ToDto(Message m) => new()
        {
            Id         = m.Id,
            SenderId   = m.SenderId,
            SenderName = m.SenderName,
            ReceiverId = m.ReceiverId,
            ChannelId  = m.ChannelId,
            Content    = m.Content,
            Type       = m.Type,
            Metadata   = m.Metadata,
            SentAt     = m.SentAt,
            IsRead     = m.IsRead,
        };

        private static string FormatTime(DateTime sentAt)
        {
            var now = DateTime.UtcNow;
            var diff = now - sentAt;

            if (diff.TotalSeconds < 60)
                return "Now";
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h";
            if (diff.TotalDays < 7)
                return sentAt.ToString("ddd");
            
            return sentAt.ToString("MMM d");
        }
    }
}
