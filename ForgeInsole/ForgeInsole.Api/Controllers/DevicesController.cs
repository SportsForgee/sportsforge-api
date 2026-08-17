using ForgeInsole.Api.Auth;
using ForgeInsole.Api.Devices;
using ForgeInsole.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeInsole.Api.Controllers
{
    // Discover / connect / disconnect physical insoles at runtime.
    [ApiController]
    [Route("api/v1/devices")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    public class DevicesController : ControllerBase
    {
        private readonly InsoleDeviceManager _manager;
        private readonly InsoleScanner _scanner;

        public DevicesController(InsoleDeviceManager manager, InsoleScanner scanner)
        {
            _manager = manager;
            _scanner = scanner;
        }

        // GET /api/v1/devices — what is connected right now
        [HttpGet]
        public ActionResult<IEnumerable<DeviceStatusDto>> GetConnected() =>
            Ok(_manager.Statuses().OrderBy(d => d.Key).Select(InsolesController.ToStatusDto));

        // GET /api/v1/devices/discover — sweep the local network for insoles
        [HttpGet("discover")]
        public async Task<ActionResult<IEnumerable<DiscoveredInsoleDto>>> Discover(
            [FromQuery] int timeoutMs, CancellationToken ct)
        {
            timeoutMs = timeoutMs <= 0 ? 250 : Math.Clamp(timeoutMs, 50, 2000);
            var found = await _scanner.ScanAsync(timeoutMs, ct);

            return Ok(found.Select(d => new DiscoveredInsoleDto(
                d.DeviceId, d.DeviceName, d.Foot, d.Firmware, d.MpuOk, d.RequiresAuth,
                _manager.IsConnected($"{d.Host}:{d.WsPort}"))));
        }

        // POST /api/v1/devices/connect — start streaming, using a password the caller supplies
        [HttpPost("connect")]
        public async Task<ActionResult<DeviceStatusDto>> Connect([FromBody] ConnectDeviceRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.DeviceId))
                return BadRequest(new { message = "deviceId is required." });

            // The client only ever knows the device by id, so the address is resolved here.
            var match = await _scanner.ResolveAsync(req.DeviceId!, ct);
            if (match is null)
                return NotFound(new { message = $"'{req.DeviceId}' is not answering on this network." });

            var device = new InsoleDeviceOptions
            {
                Host = match.Host,
                WsPort = match.WsPort,
                Password = req.Password ?? "",
                InsoleId = string.IsNullOrWhiteSpace(req.InsoleId) ? null : req.InsoleId,
            };

            _manager.Connect(device);

            // Give the handshake a moment so the caller learns about a bad password now,
            // rather than having to poll for a failure that already happened.
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(150, ct);
                var s = _manager.Statuses().FirstOrDefault(x => x.Key == device.Key);
                if (s is null) continue;
                if (s.State == DeviceConnectionState.Streaming) return Ok(InsolesController.ToStatusDto(s));
                if (s.LastError is not null)
                {
                    await _manager.DisconnectAsync(device.Key);
                    return BadRequest(new { message = s.LastError });
                }
            }

            var status = _manager.Statuses().FirstOrDefault(x => x.Key == device.Key);
            return status is null
                ? StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Device did not respond." })
                : Ok(InsolesController.ToStatusDto(status));
        }

        // POST /api/v1/devices/disconnect — omit deviceId to drop every connection
        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect([FromBody] DisconnectDeviceRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.DeviceId))
            {
                await _manager.DisconnectAllAsync();
                return Ok(new { disconnected = "all" });
            }

            // Statuses carry the insole id the device reported, which is what the client knows.
            var status = _manager.Statuses().FirstOrDefault(s =>
                string.Equals(s.InsoleId, req.DeviceId, StringComparison.OrdinalIgnoreCase));
            if (status is null) return NotFound(new { message = "Not connected." });

            var ok = await _manager.DisconnectAsync(status.Key);
            return ok ? Ok(new { disconnected = req.DeviceId }) : NotFound(new { message = "Not connected." });
        }
    }
}
