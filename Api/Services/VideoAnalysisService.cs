using System.Text.Json;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public class VideoAnalysisService : IVideoAnalysisService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly IVideoStorageService _storage;
        private readonly IVideoAnalysisAiClient _aiClient;
        private readonly IHubContext<VitalsHub> _vitalsHub;
        private readonly IHubContext<MessageHub> _messageHub;
        private readonly IConfiguration _config;

        public VideoAnalysisService(
            AppDbContext db, UserManager<AppUser> users, IVideoStorageService storage,
            IVideoAnalysisAiClient aiClient, IHubContext<VitalsHub> vitalsHub,
            IHubContext<MessageHub> messageHub, IConfiguration config)
        {
            _db        = db;
            _users     = users;
            _storage   = storage;
            _aiClient  = aiClient;
            _vitalsHub = vitalsHub;
            _messageHub = messageHub;
            _config    = config;
        }

        public async Task<VideoUpload> SaveUploadAsync(string athleteId, string uploadedByUserId, string fileName, long sizeBytes, Stream content, CancellationToken ct = default)
        {
            var path = await _storage.SaveAsync(athleteId, fileName, content, ct);

            var upload = new VideoUpload
            {
                AthleteId        = athleteId,
                UploadedByUserId = uploadedByUserId,
                FileName         = fileName,
                StoragePath      = path,
                SizeBytes        = sizeBytes,
                Status           = "Queued",
            };
            _db.VideoUploads.Add(upload);
            await _db.SaveChangesAsync(ct);
            return upload;
        }

        public async Task<bool> StartAnalysisAsync(VideoUpload upload, CancellationToken ct = default)
        {
            var apiBaseUrl = _config["Api:PublicBaseUrl"] ?? "http://localhost:5186";
            var callbackKey = _config["Video:CallbackApiKey"] ?? "";

            var payload = new RequestVideoAnalysisPayload
            {
                VideoId     = upload.Id,
                AthleteId   = upload.AthleteId,
                VideoPath   = upload.StoragePath,
                CallbackUrl = $"{apiBaseUrl}/api/videos/{upload.Id}/result?key={Uri.EscapeDataString(callbackKey)}",
            };

            var accepted = await _aiClient.RequestAnalysisAsync(payload, ct);

            upload.Status = accepted ? "Processing" : "Failed";
            await _db.SaveChangesAsync(ct);
            return accepted;
        }

        public async Task<VideoAnalysisResult> HandleCallbackAsync(VideoAnalysisCallbackRequest payload, CancellationToken ct = default)
        {
            var upload = await _db.VideoUploads.FirstOrDefaultAsync(v => v.Id == payload.VideoId, ct)
                ?? throw new InvalidOperationException($"Unknown videoId {payload.VideoId}");

            // The user cancelled while the AI pipeline was still running in the background
            // (it has no hard-kill hook) — discard this late result instead of resurrecting it.
            // Caller (the internal callback endpoint) doesn't use the return value.
            if (upload.Status == "Cancelled")
                return new VideoAnalysisResult { VideoUploadId = upload.Id, ResultJson = "{}" };

            upload.Status = payload.Status;
            if (payload.DurationSeconds.HasValue) upload.DurationSeconds = payload.DurationSeconds;

            var result = new VideoAnalysisResult
            {
                VideoUploadId     = upload.Id,
                ResultJson        = JsonSerializer.Serialize(payload),
                TopSpeedKmh       = payload.SpeedMetrics?.Calibration?.Method != "uncalibrated" ? payload.SpeedMetrics?.TopSpeedKmh : null,
                GaitBalanceScore  = payload.GaitBalance?.Score,
                SymmetryScore     = payload.PoseMetrics?.SymmetryScore,
                AnomalyCount      = payload.Anomalies.Count,
                CalibrationMethod = payload.SpeedMetrics?.Calibration?.Method,
            };
            _db.VideoAnalysisResults.Add(result);
            await _db.SaveChangesAsync(ct);

            await _vitalsHub.Clients.Group($"athlete-{upload.AthleteId}")
                .SendAsync("VideoAnalysisComplete", new
                {
                    videoId = upload.Id,
                    status  = upload.Status,
                    topSpeedKmh = result.TopSpeedKmh,
                    gaitBalanceScore = result.GaitBalanceScore,
                    anomalyCount = result.AnomalyCount,
                }, ct);

            var seriousAnomalies = payload.Anomalies.Where(a => a.Severity is "Warning" or "Critical").ToList();
            if (seriousAnomalies.Count > 0)
                await RaiseAnomalyAlertAsync(upload, seriousAnomalies, ct);

            return result;
        }

        // Reuses the existing messaging system (Message.Type == "health_alert", already
        // reserved for this) rather than inventing a parallel alert mechanism — coach/doctor
        // dashboards read alerts the same way they read any other message.
        private async Task RaiseAnomalyAlertAsync(VideoUpload upload, List<VideoAnomalyDto> anomalies, CancellationToken ct)
        {
            var athlete = await _users.FindByIdAsync(upload.AthleteId);
            if (athlete == null || string.IsNullOrEmpty(athlete.Organisation)) return;

            var recipients = await _db.Users
                .Where(u => (u.SfRole == "coach" || u.SfRole == "doctor") && u.Organisation == athlete.Organisation)
                .ToListAsync(ct);

            var summary = string.Join("; ", anomalies.Select(a => $"{a.Type} ({a.Severity}) at {a.TimestampSeconds:0.0}s — {a.Description}"));
            var content = $"AI video analysis flagged {anomalies.Count} issue(s) for {athlete.FirstName} {athlete.LastName}: {summary}";
            var metadata = JsonSerializer.Serialize(new { videoId = upload.Id, athleteId = upload.AthleteId, anomalies });

            foreach (var recipient in recipients)
            {
                var msg = new Message
                {
                    SenderId   = athlete.Id,
                    SenderName = "SportsForge AI",
                    ReceiverId = recipient.Id,
                    Content    = content,
                    Type       = "health_alert",
                    Metadata   = metadata,
                };
                _db.Messages.Add(msg);
                await _db.SaveChangesAsync(ct);

                await _messageHub.Clients.Group($"user-{recipient.Id}").SendAsync("NewMessage", new
                {
                    id = msg.Id, senderId = msg.SenderId, senderName = msg.SenderName,
                    receiverId = msg.ReceiverId, content = msg.Content, type = msg.Type,
                    metadata = msg.Metadata, sentAt = msg.SentAt, isRead = msg.IsRead,
                }, ct);
            }
        }

        public async Task<List<VideoUploadDto>> GetUploadsForAthleteAsync(string athleteId)
        {
            var uploads = await _db.VideoUploads
                .Where(v => v.AthleteId == athleteId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            var results = await _db.VideoAnalysisResults
                .Where(r => uploads.Select(u => u.Id).Contains(r.VideoUploadId))
                .GroupBy(r => r.VideoUploadId)
                .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
                .ToListAsync();

            return uploads.Select(u =>
            {
                var result = results.FirstOrDefault(r => r.VideoUploadId == u.Id);
                return new VideoUploadDto
                {
                    Id               = u.Id,
                    FileName         = u.FileName,
                    Status           = u.Status,
                    DurationSeconds  = u.DurationSeconds,
                    CreatedAt        = u.CreatedAt,
                    VideoUrl         = _storage.GetPublicUrl(u.AthleteId, u.StoragePath),
                    TopSpeedKmh      = result?.TopSpeedKmh,
                    GaitBalanceScore = result?.GaitBalanceScore,
                    SymmetryScore    = result?.SymmetryScore,
                    AnomalyCount     = result?.AnomalyCount,
                };
            }).ToList();
        }

        public Task<VideoUpload?> GetUploadAsync(string videoId) =>
            _db.VideoUploads.FirstOrDefaultAsync(v => v.Id == videoId);

        public async Task<bool> CancelAsync(string videoId)
        {
            var upload = await _db.VideoUploads.FirstOrDefaultAsync(v => v.Id == videoId);
            if (upload == null || upload.Status is "Complete" or "Failed" or "Cancelled") return false;

            upload.Status = "Cancelled";
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(string videoId)
        {
            var upload = await _db.VideoUploads.FirstOrDefaultAsync(v => v.Id == videoId);
            if (upload == null) return false;

            var results = _db.VideoAnalysisResults.Where(r => r.VideoUploadId == videoId);
            _db.VideoAnalysisResults.RemoveRange(results);
            _db.VideoUploads.Remove(upload);
            await _db.SaveChangesAsync();

            _storage.DeleteVideoFiles(upload.AthleteId, videoId, upload.StoragePath);
            return true;
        }

        public Task<VideoAnalysisResult?> GetResultAsync(string videoId) =>
            _db.VideoAnalysisResults
                .Where(r => r.VideoUploadId == videoId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

        public async Task<VideoComparisonDto?> GetComparisonAsync(string videoId)
        {
            var current = await GetResultAsync(videoId);
            if (current == null) return null;

            var upload = await _db.VideoUploads.FindAsync(videoId);
            if (upload == null) return null;

            // The athlete's other Complete clips — denormalized columns on
            // VideoAnalysisResult already carry the numbers we need, so no ResultJson
            // parsing here.
            var history = await (
                from r in _db.VideoAnalysisResults
                join u in _db.VideoUploads on r.VideoUploadId equals u.Id
                where u.AthleteId == upload.AthleteId && r.VideoUploadId != videoId
                select r
            ).ToListAsync();

            return new VideoComparisonDto
            {
                ClipsComparedCount = history.Count,
                GaitBalance = BuildMetricComparison(current.GaitBalanceScore, history.Select(h => h.GaitBalanceScore)),
                Symmetry    = BuildMetricComparison(current.SymmetryScore, history.Select(h => h.SymmetryScore)),
                // TopSpeedKmh is already null on both current and history rows for
                // uncalibrated clips (see HandleCallbackAsync) — never fabricate a speed.
                TopSpeedKmh = BuildMetricComparison(current.TopSpeedKmh, history.Select(h => h.TopSpeedKmh)),
            };
        }

        private static MetricComparisonDto? BuildMetricComparison(double? current, IEnumerable<double?> historyValues)
        {
            if (current == null) return null;

            var validHistory = historyValues.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (validHistory.Count == 0) return null; // no history to compare against yet — never fabricate one

            var best = validHistory.Max();
            return new MetricComparisonDto
            {
                Current           = current.Value,
                PersonalBest      = best,
                Delta             = Math.Round(current.Value - best, 1),
                IsNewPersonalBest = current.Value > best,
            };
        }
    }
}
