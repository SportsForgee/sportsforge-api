using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Hubs
{
    [Authorize]
    public class MessageHub : Hub
    {
        private readonly AppDbContext _db;

        public MessageHub(AppDbContext db) => _db = db;

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();

            // Each user joins their personal group so others can target them
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            // Re-join all channels the user is a member of
            var channelIds = await _db.ChannelMembers
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ChannelId)
                .ToListAsync();

            foreach (var cId in channelIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"channel-{cId}");

            await base.OnConnectedAsync();
        }

        // Coach → Athlete direct message (or any stakeholder → any)
        public async Task SendDirectMessage(string receiverId, string content,
                                            string type = "text", string? metadata = null)
        {
            var senderId   = GetUserId();
            var senderName = GetSenderName();

            var msg = new Message
            {
                SenderId   = senderId,
                SenderName = senderName,
                ReceiverId = receiverId,
                Content    = content,
                Type       = type,
                Metadata   = metadata,
            };

            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var dto = ToDto(msg);
            await Clients.Group($"user-{senderId}").SendAsync("NewMessage", dto);
            await Clients.Group($"user-{receiverId}").SendAsync("NewMessage", dto);
        }

        // Coach sends a structured drill card to an athlete
        public async Task SendDrillCard(string athleteId, DrillCard drill)
        {
            var metadata = System.Text.Json.JsonSerializer.Serialize(drill);
            await SendDirectMessage(athleteId, drill.Title, "drill", metadata);
        }

        // Group / team / medical channel message
        public async Task SendChannelMessage(string channelId, string content,
                                             string type = "text", string? metadata = null)
        {
            var senderId = GetUserId();

            var isMember = await _db.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == senderId);

            if (!isMember)
            {
                throw new HubException("Not a member of this channel.");
            }

            var msg = new Message
            {
                SenderId   = senderId,
                SenderName = GetSenderName(),
                ChannelId  = channelId,
                Content    = content,
                Type       = type,
                Metadata   = metadata,
            };

            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            await Clients.Group($"channel-{channelId}").SendAsync("NewMessage", ToDto(msg));
        }

        // Mark a direct message as read
        public async Task MarkRead(int messageId)
        {
            var userId = GetUserId();
            var msg    = await _db.Messages.FindAsync(messageId);

            if (msg != null && msg.ReceiverId == userId && !msg.IsRead)
            {
                msg.IsRead = true;
                await _db.SaveChangesAsync();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private string GetUserId() =>
            Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Not authenticated.");

        private string GetSenderName()
        {
            var given  = Context.User?.FindFirst(ClaimTypes.GivenName)?.Value  ?? "";
            var family = Context.User?.FindFirst(ClaimTypes.Surname)?.Value    ?? "";
            return $"{given} {family}".Trim();
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
    }
}
