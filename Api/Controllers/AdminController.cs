using System.Security.Claims;
using System.Text.Json;
using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private static readonly PlatformSetting[] DefaultSettings =
        {
            new() { Key = "scouting.enabled",        Value = "true",  Description = "Allow athletes to make their profile visible to scouts." },
            new() { Key = "video.analysis.enabled",  Value = "true",  Description = "Accept new video uploads for AI analysis." },
            new() { Key = "registration.open",       Value = "true",  Description = "Allow new accounts to be registered." },
            new() { Key = "messaging.enabled",       Value = "true",  Description = "Enable in-app messaging between users." },
            new() { Key = "hardware.sync.enabled",   Value = "true",  Description = "Poll Forge Insole and KHOI devices for telemetry." },
        };

        private readonly UserManager<AppUser> _users;
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public AdminController(UserManager<AppUser> users, AppDbContext db, IAuditService audit,
                               IHttpClientFactory httpFactory, IConfiguration config)
        {
            _users = users;
            _db = db;
            _audit = audit;
            _httpFactory = httpFactory;
            _config = config;
        }

        // ── STATS ────────────────────────────────────────────────────────────

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            if (await GetAdminAsync() == null) return Forbid();

            var now = DateTime.UtcNow;
            var users = await _users.Users.ToListAsync();

            var activeSessions = await _db.AuditLogs
                .Where(a => a.Action == "login.success" && a.CreatedAt >= now.AddHours(-24))
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync();

            return Ok(new AdminStatsDto
            {
                TotalUsers        = users.Count,
                UsersByRole       = users.GroupBy(u => string.IsNullOrWhiteSpace(u.SfRole) ? "unknown" : u.SfRole)
                                         .ToDictionary(g => g.Key, g => g.Count()),
                SuspendedUsers    = users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow),
                NewUsersLast7Days = users.Count(u => u.CreatedAt >= now.AddDays(-7)),
                ActiveSessionsLast24Hours = activeSessions,
                TotalOrganisations = users.Where(u => !string.IsNullOrWhiteSpace(u.Organisation))
                                          .Select(u => u.Organisation).Distinct().Count(),
                TotalVideos         = await _db.VideoUploads.CountAsync(),
                VideosAnalysedToday = await _db.VideoAnalysisResults.CountAsync(r => r.CreatedAt >= now.Date),
                TotalDevices        = await _db.Devices.CountAsync(),
                AthletesVisibleToScouts = users.Count(u => u.SfRole == "athlete" && u.IsVisibleToScouts),
            });
        }

        // ── USER MANAGEMENT ──────────────────────────────────────────────────

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] string? search,
                                                  [FromQuery] string? status)
        {
            if (await GetAdminAsync() == null) return Forbid();

            var query = _users.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.SfRole == role);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    u.FirstName.Contains(term) ||
                    u.LastName.Contains(term) ||
                    (u.Email != null && u.Email.Contains(term)) ||
                    (u.Organisation != null && u.Organisation.Contains(term)));
            }

            var list = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            if (status == "suspended")
                list = list.Where(u => IsSuspended(u)).ToList();
            else if (status == "active")
                list = list.Where(u => !IsSuspended(u)).ToList();

            var ids = list.Select(u => u.Id).ToList();
            var lastLogins = await _db.AuditLogs
                .Where(a => a.Action == "login.success" && a.UserId != null && ids.Contains(a.UserId))
                .GroupBy(a => a.UserId!)
                .Select(g => new { UserId = g.Key, Last = g.Max(a => a.CreatedAt) })
                .ToDictionaryAsync(x => x.UserId, x => x.Last);

            return Ok(list.Select(u => new AdminUserDto
            {
                Id             = u.Id,
                Name           = $"{u.FirstName} {u.LastName}".Trim(),
                Email          = u.Email,
                Role           = u.SfRole,
                Organisation   = u.Organisation,
                CreatedAt      = u.CreatedAt,
                IsSuspended    = IsSuspended(u),
                SuspendedUntil = IsSuspended(u) ? u.LockoutEnd : null,
                LastLoginAt    = lastLogins.TryGetValue(u.Id, out var last) ? last : null,
            }));
        }

        [HttpPost("users/{id}/suspend")]
        public async Task<IActionResult> SuspendUser(string id, [FromBody] SuspendUserRequest? req)
        {
            var admin = await GetAdminAsync();
            if (admin == null) return Forbid();

            if (admin.Id == id)
                return BadRequest(new { error = "You cannot suspend your own account." });

            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound(new { error = "User not found." });

            var until = req?.Days is int days && days > 0
                ? DateTimeOffset.UtcNow.AddDays(days)
                : DateTimeOffset.MaxValue;

            await _users.SetLockoutEnabledAsync(user, true);
            var result = await _users.SetLockoutEndDateAsync(user, until);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await _audit.LogAsync("admin", "user.suspended",
                $"{user.Email} suspended by {admin.Email}. Reason: {req?.Reason ?? "not given"}.", admin);

            return Ok(new { id = user.Id, isSuspended = true, suspendedUntil = until });
        }

        [HttpPost("users/{id}/reactivate")]
        public async Task<IActionResult> ReactivateUser(string id)
        {
            var admin = await GetAdminAsync();
            if (admin == null) return Forbid();

            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound(new { error = "User not found." });

            var result = await _users.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await _audit.LogAsync("admin", "user.reactivated", $"{user.Email} reactivated by {admin.Email}.", admin);

            return Ok(new { id = user.Id, isSuspended = false });
        }

        // ── AI MONITOR ───────────────────────────────────────────────────────

        [HttpGet("ai")]
        public async Task<IActionResult> GetAiMonitor(CancellationToken ct)
        {
            if (await GetAdminAsync() == null) return Forbid();

            var baseUrl = _config["AiService:BaseUrl"] ?? "http://localhost:8001";
            var dto = new AiMonitorDto { BaseUrl = baseUrl };

            var started = DateTime.UtcNow;
            try
            {
                var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                using var res = await client.GetAsync($"{baseUrl.TrimEnd('/')}/video/health", ct);
                dto.LatencyMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                dto.ServiceReachable = res.IsSuccessStatusCode;

                if (res.IsSuccessStatusCode)
                {
                    using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                    var root = json.RootElement;
                    dto.OpenCvAvailable    = root.TryGetProperty("opencvAvailable", out var o) && o.GetBoolean();
                    dto.MediapipeAvailable = root.TryGetProperty("mediapipeAvailable", out var m) && m.GetBoolean();
                    dto.ModelReady         = root.TryGetProperty("modelReady", out var r) && r.GetBoolean();
                }
                else
                {
                    dto.Error = $"AI service returned {(int)res.StatusCode}.";
                }
            }
            catch (Exception ex)
            {
                dto.ServiceReachable = false;
                dto.Error = ex.Message;
            }

            var statuses = await _db.VideoUploads
                .GroupBy(v => v.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int CountFor(string s) => statuses.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

            dto.JobsQueued     = CountFor("Queued");
            dto.JobsProcessing = CountFor("Processing");
            dto.JobsComplete   = CountFor("Complete");
            dto.JobsFailed     = CountFor("Failed");
            dto.AnalysesLast24Hours = await _db.VideoAnalysisResults
                .CountAsync(r => r.CreatedAt >= DateTime.UtcNow.AddHours(-24), ct);

            return Ok(dto);
        }

        // ── AUDIT LOGS ───────────────────────────────────────────────────────

        [HttpGet("audit")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] string? category, [FromQuery] string? search,
                                                      [FromQuery] int take = 100)
        {
            if (await GetAdminAsync() == null) return Forbid();

            var query = _db.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(a => a.Category == category);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    a.Action.Contains(term) ||
                    (a.UserEmail != null && a.UserEmail.Contains(term)) ||
                    (a.Details != null && a.Details.Contains(term)));
            }

            var logs = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(Math.Clamp(take, 1, 500))
                .Select(a => new AuditLogDto
                {
                    Id        = a.Id,
                    Category  = a.Category,
                    Action    = a.Action,
                    Details   = a.Details,
                    Success   = a.Success,
                    UserEmail = a.UserEmail,
                    UserRole  = a.UserRole,
                    IpAddress = a.IpAddress,
                    CreatedAt = a.CreatedAt,
                })
                .ToListAsync();

            return Ok(logs);
        }

        // ── SETTINGS ─────────────────────────────────────────────────────────

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            if (await GetAdminAsync() == null) return Forbid();

            var existing = await _db.PlatformSettings.ToListAsync();

            var missing = DefaultSettings.Where(d => existing.All(e => e.Key != d.Key)).ToList();
            if (missing.Count > 0)
            {
                _db.PlatformSettings.AddRange(missing);
                await _db.SaveChangesAsync();
                existing.AddRange(missing);
            }

            return Ok(existing.OrderBy(s => s.Key).Select(s => new PlatformSettingDto
            {
                Key         = s.Key,
                Value       = s.Value,
                Description = s.Description,
                UpdatedAt   = s.UpdatedAt,
            }));
        }

        [HttpPut("settings/{key}")]
        public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest req)
        {
            var admin = await GetAdminAsync();
            if (admin == null) return Forbid();

            var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null) return NotFound(new { error = "Setting not found." });

            var previous = setting.Value;
            setting.Value = req.Value;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedByUserId = admin.Id;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("admin", "setting.updated",
                $"{key} changed from '{previous}' to '{req.Value}' by {admin.Email}.", admin);

            return Ok(new PlatformSettingDto
            {
                Key         = setting.Key,
                Value       = setting.Value,
                Description = setting.Description,
                UpdatedAt   = setting.UpdatedAt,
            });
        }

        // ── HELPERS ──────────────────────────────────────────────────────────

        private static bool IsSuspended(AppUser u) =>
            u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;

        private async Task<AppUser?> GetAdminAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var user = await _users.FindByIdAsync(userId);
            return user?.SfRole == "admin" ? user : null;
        }
    }
}
