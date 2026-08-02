using System.Security.Claims;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/devices")]
    public class DevicesController : ControllerBase
    {
        private readonly IHardwareTelemetryService _telemetry;

        public DevicesController(IHardwareTelemetryService telemetry) => _telemetry = telemetry;

        // GET /api/devices — the calling athlete's own paired devices
        [HttpGet]
        public async Task<IActionResult> GetMyDevices()
        {
            var devices = await _telemetry.GetDevicesForAthleteAsync(GetUserId());
            return Ok(devices);
        }

        // GET /api/devices/athlete/{athleteId} — coach/doctor/scout view of another athlete's devices
        [HttpGet("athlete/{athleteId}")]
        public async Task<IActionResult> GetDevicesForAthlete(string athleteId)
        {
            var devices = await _telemetry.GetDevicesForAthleteAsync(athleteId);
            return Ok(devices);
        }

        // POST /api/devices/pair — register/pair a device to the calling athlete
        [HttpPost("pair")]
        public async Task<IActionResult> Pair([FromBody] PairDeviceRequest req)
        {
            var device = await _telemetry.GetOrRegisterDeviceAsync(GetUserId(), req.Type, req.SerialNumber, req.FirmwareVersion);
            var devices = await _telemetry.GetDevicesForAthleteAsync(GetUserId());
            return Ok(devices.First(d => d.Id == device.Id));
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
