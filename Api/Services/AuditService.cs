using Api.Data;
using Api.Models;

namespace Api.Services
{
    public interface IAuditService
    {
        Task LogAsync(string category, string action, string? details = null,
                      AppUser? user = null, string? email = null, bool success = true);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditService> _log;

        public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> log)
        {
            _db = db;
            _http = http;
            _log = log;
        }

        public async Task LogAsync(string category, string action, string? details = null,
                                   AppUser? user = null, string? email = null, bool success = true)
        {
            var entry = new AuditLog
            {
                Category  = category,
                Action    = action,
                Details   = details,
                Success   = success,
                UserId    = user?.Id,
                UserEmail = user?.Email ?? email,
                UserRole  = user?.SfRole,
                IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            };

            try
            {
                _db.AuditLogs.Add(entry);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Auditing must never break the request it is recording.
                _log.LogWarning(ex, "Failed to write audit log {Action}.", action);
            }
        }
    }
}
