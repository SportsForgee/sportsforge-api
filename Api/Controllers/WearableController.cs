using System.Security.Claims;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/wearable")]
    public class WearableController : ControllerBase
    {
        private readonly IHardwareTelemetryService _telemetry;

        public WearableController(IHardwareTelemetryService telemetry) => _telemetry = telemetry;

        // POST /api/wearable/ingest — khoi-wearable-sim (today) / KhoiSyncService (later)
        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] WearableIngestRequest req)
        {
            if (req.Readings.Count == 0) return BadRequest(new { error = "No readings provided." });

            req.AthleteId = GetUserId();

            await _telemetry.IngestWearableReadingsAsync(req);
            return Ok(new { accepted = req.Readings.Count });
        }

        // GET /api/wearable/latest/me — the calling athlete's own latest reading
        [HttpGet("latest/me")]
        public async Task<IActionResult> GetMyLatest()
        {
            var reading = await _telemetry.GetLatestWearableReadingAsync(GetUserId());
            return reading == null ? NotFound() : Ok(reading);
        }

        // GET /api/wearable/latest/{athleteId} — coach/doctor/scout view of another athlete
        [HttpGet("latest/{athleteId}")]
        public async Task<IActionResult> GetLatest(string athleteId)
        {
            var reading = await _telemetry.GetLatestWearableReadingAsync(athleteId);
            return reading == null ? NotFound() : Ok(reading);
        }

        // GET /api/wearable/summary/me
        [HttpGet("summary/me")]
        public async Task<IActionResult> GetMySummary()
        {
            var summary = await _telemetry.GetWearableSummaryAsync(GetUserId(), TimeSpan.FromMinutes(30));
            return Ok(summary);
        }

        // GET /api/wearable/summary/{athleteId}
        [HttpGet("summary/{athleteId}")]
        public async Task<IActionResult> GetSummary(string athleteId)
        {
            var summary = await _telemetry.GetWearableSummaryAsync(athleteId, TimeSpan.FromMinutes(30));
            return Ok(summary);
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
