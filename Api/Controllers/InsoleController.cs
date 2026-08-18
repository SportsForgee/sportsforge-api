using System.Security.Claims;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/insole")]
    public class InsoleController : ControllerBase
    {
        private readonly IHardwareTelemetryService _telemetry;

        public InsoleController(IHardwareTelemetryService telemetry) => _telemetry = telemetry;

        // POST /api/insole/ingest — forge-insole-sim (today) / athlete-mobile BLE bridge (later)
        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] InsoleIngestRequest req)
        {
            if (req.Readings.Count == 0) return BadRequest(new { error = "No readings provided." });

            // The caller authenticates as the athlete the insole is paired to — trust the
            // token's identity, not the client-supplied athleteId, to avoid one athlete's
            // device posting data under another athlete's id.
            req.AthleteId = GetUserId();

            await _telemetry.IngestInsoleReadingsAsync(req);
            return Ok(new { accepted = req.Readings.Count });
        }

        // GET /api/insole/latest/me — the calling athlete's own latest reading
        [HttpGet("latest/me")]
        public async Task<IActionResult> GetMyLatest()
        {
            var reading = await _telemetry.GetLatestInsoleReadingAsync(GetUserId());
            return reading == null ? NotFound() : Ok(reading);
        }

        // GET /api/insole/latest/{athleteId} — coach/doctor/scout view of another athlete
        [HttpGet("latest/{athleteId}")]
        public async Task<IActionResult> GetLatest(string athleteId)
        {
            var reading = await _telemetry.GetLatestInsoleReadingAsync(athleteId);
            return reading == null ? NotFound() : Ok(reading);
        }

        // GET /api/insole/summary/me
        [HttpGet("summary/me")]
        public async Task<IActionResult> GetMySummary()
        {
            var summary = await _telemetry.GetInsoleSummaryAsync(GetUserId(), TimeSpan.FromMinutes(30));
            return Ok(summary);
        }

        // GET /api/insole/summary/{athleteId}
        [HttpGet("summary/{athleteId}")]
        public async Task<IActionResult> GetSummary(string athleteId)
        {
            var summary = await _telemetry.GetInsoleSummaryAsync(athleteId, TimeSpan.FromMinutes(30));
            return Ok(summary);
        }

        // GET /api/insole/trend/me?days=7 — daily aggregates + biomechanics for the charts
        [HttpGet("trend/me")]
        public async Task<IActionResult> GetMyTrend([FromQuery] int days)
            => Ok(await _telemetry.GetInsoleTrendAsync(GetUserId(), days <= 0 ? 7 : days));

        // GET /api/insole/trend/{athleteId}?days=7 — coach/doctor view
        [HttpGet("trend/{athleteId}")]
        public async Task<IActionResult> GetTrend(string athleteId, [FromQuery] int days)
            => Ok(await _telemetry.GetInsoleTrendAsync(athleteId, days <= 0 ? 7 : days));

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
