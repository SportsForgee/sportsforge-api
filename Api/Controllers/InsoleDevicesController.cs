using System.Security.Claims;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    public record ConnectInsoleRequest(string DeviceId, string Password);

    // Lets the athlete app find and connect a Forge Insole without ever seeing a network
    // address. It forwards to ForgeInsole.Api (which owns the WebSocket link to the
    // hardware), so the app authenticates with its normal JWT and never needs the partner
    // API key or the device's IP.
    [Authorize]
    [ApiController]
    [Route("api/insole-devices")]
    public class InsoleDevicesController : ControllerBase
    {
        private readonly IForgeInsoleClient _forge;
        private readonly IHardwareTelemetryService _telemetry;

        public InsoleDevicesController(IForgeInsoleClient forge, IHardwareTelemetryService telemetry)
        {
            _forge = forge;
            _telemetry = telemetry;
        }

        // GET /api/insole-devices/discover — insoles visible on the network right now
        [HttpGet("discover")]
        public async Task<IActionResult> Discover(CancellationToken ct)
        {
            try
            {
                return Ok(await _forge.DiscoverAsync(ct));
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "The Forge Insole service isn't reachable." });
            }
        }

        // GET /api/insole-devices — what is streaming right now
        [HttpGet]
        public async Task<IActionResult> Connected(CancellationToken ct)
        {
            try
            {
                return Ok(await _forge.GetConnectedAsync(ct));
            }
            catch (HttpRequestException)
            {
                return Ok(Array.Empty<object>());
            }
        }

        // POST /api/insole-devices/connect — { deviceId, password }
        [HttpPost("connect")]
        public async Task<IActionResult> Connect([FromBody] ConnectInsoleRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.DeviceId))
                return BadRequest(new { message = "Pick a device first." });

            try
            {
                var (ok, error) = await _forge.ConnectAsync(req.DeviceId, req.Password ?? "", ct);
                if (!ok) return BadRequest(new { message = error });

                // Pair it to whoever connected it, so the sync bridge starts pulling its
                // readings immediately instead of skipping an unpaired insole.
                await _telemetry.GetOrRegisterDeviceAsync(GetUserId(), "Insole", req.DeviceId, null);

                return Ok(new { connected = req.DeviceId });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "The Forge Insole service isn't reachable." });
            }
        }

        // POST /api/insole-devices/disconnect — { deviceId }, or empty to drop all
        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromBody] ConnectInsoleRequest? req, CancellationToken ct)
        {
            try
            {
                var ok = await _forge.DisconnectAsync(req?.DeviceId, ct);
                return ok ? Ok(new { disconnected = req?.DeviceId ?? "all" })
                          : NotFound(new { message = "That device isn't connected." });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "The Forge Insole service isn't reachable." });
            }
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
