using System.Security.Claims;
using System.Text.Json;
using Api.Models;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/videos")]
    public class VideosController : ControllerBase
    {
        private const long MaxSizeBytes = 200L * 1024 * 1024; // 200 MB
        private static readonly string[] AllowedExtensions = { ".mp4", ".mov" };

        private readonly IVideoAnalysisService _videos;
        private readonly UserManager<AppUser> _users;
        private readonly IConfiguration _config;

        public VideosController(IVideoAnalysisService videos, UserManager<AppUser> users, IConfiguration config)
        {
            _videos = videos;
            _users  = users;
            _config = config;
        }

        // POST /api/videos?athleteId=... — athleteId optional; coaches use it to upload
        // on behalf of one of their athletes, athletes omit it to upload their own.
        [Authorize]
        [HttpPost]
        [RequestSizeLimit(MaxSizeBytes)]
        public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? athleteId)
        {
            if (file == null || file.Length == 0) return BadRequest(new { error = "No file provided." });
            if (file.Length > MaxSizeBytes) return BadRequest(new { error = "File exceeds the 200 MB limit." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return BadRequest(new { error = "Only .mp4 and .mov files are supported." });

            var requesterId = GetUserId();
            var targetAthleteId = requesterId;

            if (!string.IsNullOrEmpty(athleteId) && athleteId != requesterId)
            {
                var requester = await _users.FindByIdAsync(requesterId);
                var athlete   = await _users.FindByIdAsync(athleteId);
                if (requester == null || athlete == null || athlete.SfRole != "athlete") return NotFound(new { error = "Athlete not found." });
                if (requester.SfRole != "coach" || requester.Organisation != athlete.Organisation)
                    return Forbid();
                targetAthleteId = athleteId;
            }

            await using var stream = file.OpenReadStream();
            var upload = await _videos.SaveUploadAsync(targetAthleteId, requesterId, file.FileName, file.Length, stream);
            return Ok(new VideoUploadDto { Id = upload.Id, FileName = upload.FileName, Status = upload.Status, CreatedAt = upload.CreatedAt });
        }

        // GET /api/videos/me
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyVideos()
        {
            var uploads = await _videos.GetUploadsForAthleteAsync(GetUserId());
            return Ok(uploads);
        }

        // GET /api/videos/athlete/{athleteId} — coach/doctor view
        [Authorize]
        [HttpGet("athlete/{athleteId}")]
        public async Task<IActionResult> GetAthleteVideos(string athleteId)
        {
            var uploads = await _videos.GetUploadsForAthleteAsync(athleteId);
            return Ok(uploads);
        }

        // POST /api/videos/{id}/analyze — kick off analysis
        [Authorize]
        [HttpPost("{id}/analyze")]
        public async Task<IActionResult> Analyze(string id)
        {
            var upload = await _videos.GetUploadAsync(id);
            if (upload == null) return NotFound();
            if (!await CanAccessAsync(upload)) return Forbid();

            var accepted = await _videos.StartAnalysisAsync(upload);
            return accepted ? Accepted(new { status = "Processing" }) : StatusCode(502, new { error = "AI service unavailable." });
        }

        // POST /api/videos/{id}/cancel — stop processing (best-effort; see CancelAsync)
        [Authorize]
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(string id)
        {
            var upload = await _videos.GetUploadAsync(id);
            if (upload == null) return NotFound();
            if (!await CanAccessAsync(upload)) return Forbid();

            var cancelled = await _videos.CancelAsync(id);
            return cancelled ? Ok(new { status = "Cancelled" }) : BadRequest(new { error = "Video is not in a cancellable state." });
        }

        // DELETE /api/videos/{id} — removes the DB record and the files on disk
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var upload = await _videos.GetUploadAsync(id);
            if (upload == null) return NotFound();
            if (!await CanAccessAsync(upload)) return Forbid();

            await _videos.DeleteAsync(id);
            return NoContent();
        }

        // GET /api/videos/{id}/result — full result JSON
        [Authorize]
        [HttpGet("{id}/result")]
        public async Task<IActionResult> GetResult(string id)
        {
            var upload = await _videos.GetUploadAsync(id);
            if (upload == null) return NotFound();
            if (!await CanAccessAsync(upload)) return Forbid();

            var result = await _videos.GetResultAsync(id);
            if (result == null) return NotFound(new { error = "Not analyzed yet.", status = upload.Status });

            // ResultJson was stored via a raw JsonSerializer.Serialize call (PascalCase,
            // matching the C# properties) — deserialize and return through Ok() so the
            // MVC pipeline's camelCase policy applies, matching every frontend's contract.
            var payload = JsonSerializer.Deserialize<VideoAnalysisCallbackRequest>(result.ResultJson);
            return Ok(payload);
        }

        // GET /api/videos/{id}/compare — this clip vs the athlete's own personal best
        [Authorize]
        [HttpGet("{id}/compare")]
        public async Task<IActionResult> GetComparison(string id)
        {
            var upload = await _videos.GetUploadAsync(id);
            if (upload == null) return NotFound();
            if (!await CanAccessAsync(upload)) return Forbid();

            var comparison = await _videos.GetComparisonAsync(id);
            if (comparison == null) return NotFound(new { error = "Not analyzed yet.", status = upload.Status });
            return Ok(comparison);
        }

        // GET /api/videos/compare-clips?a={id}&b={id} — two specific clips picked by the
        // caller (e.g. "before" vs "after" technique work), side by side. Distinct from
        // {id}/compare above, which always compares against the athlete's personal best.
        [Authorize]
        [HttpGet("compare-clips")]
        public async Task<IActionResult> GetClipPairComparison([FromQuery] string a, [FromQuery] string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b)
                return BadRequest(new { error = "Provide two different video ids (query params 'a' and 'b')." });

            var uploadA = await _videos.GetUploadAsync(a);
            var uploadB = await _videos.GetUploadAsync(b);
            if (uploadA == null || uploadB == null) return NotFound();
            if (!await CanAccessAsync(uploadA) || !await CanAccessAsync(uploadB)) return Forbid();

            var comparison = await _videos.GetClipPairComparisonAsync(a, b);
            if (comparison == null) return NotFound(new { error = "Both clips must be fully analyzed." });
            return Ok(comparison);
        }

        // POST /api/videos/{id}/result?key=... — internal callback from ai-service.
        // API-key protected (shared secret), not JWT — the caller is a service, not a user.
        [AllowAnonymous]
        [HttpPost("{id}/result")]
        public async Task<IActionResult> ReceiveResult(string id, [FromQuery] string key, [FromBody] VideoAnalysisCallbackRequest payload)
        {
            var expectedKey = _config["Video:CallbackApiKey"] ?? "";
            if (string.IsNullOrEmpty(expectedKey) || key != expectedKey) return Unauthorized();
            if (payload.VideoId != id) return BadRequest(new { error = "videoId mismatch." });

            await _videos.HandleCallbackAsync(payload);
            return Ok();
        }

        private async Task<bool> CanAccessAsync(VideoUpload upload)
        {
            var requesterId = GetUserId();
            if (requesterId == upload.AthleteId) return true;

            var requester = await _users.FindByIdAsync(requesterId);
            var athlete   = await _users.FindByIdAsync(upload.AthleteId);
            if (requester == null || athlete == null) return false;

            return requester.SfRole is "coach" or "doctor" or "admin" && requester.Organisation == athlete.Organisation;
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
