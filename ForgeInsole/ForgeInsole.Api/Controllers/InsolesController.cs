using System.Text.Json;
using ForgeInsole.Api.Auth;
using ForgeInsole.Api.Devices;
using ForgeInsole.Api.Dtos;
using ForgeInsole.Api.Services;
using ForgeInsole.Data;
using ForgeInsole.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForgeInsole.Api.Controllers
{
    [ApiController]
    [Route("api/v1/insoles")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName)]
    public class InsolesController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly ForgeInsoleDbContext _db;
        private readonly TelemetryBroadcaster _broadcaster;
        private readonly DeviceRegistry _devices;

        public InsolesController(ForgeInsoleDbContext db, TelemetryBroadcaster broadcaster, DeviceRegistry devices)
        {
            _db = db;
            _broadcaster = broadcaster;
            _devices = devices;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InsoleDto>>> GetInsoles(CancellationToken ct)
        {
            var insoles = await _db.Insoles.AsNoTracking().OrderBy(i => i.InsoleId).ToListAsync(ct);
            return Ok(insoles.Select(ToDto));
        }

        // Literal segment, so it takes routing precedence over "{id}" and can never be
        // shadowed by an insole literally called "devices".
        [HttpGet("devices")]
        public ActionResult<IEnumerable<DeviceStatusDto>> GetDevices()
            => Ok(_devices.All().OrderBy(d => d.Key).Select(ToStatusDto));

        [HttpGet("{id}")]
        public async Task<ActionResult<InsoleDto>> GetInsole(string id, CancellationToken ct)
        {
            var insole = await _db.Insoles.AsNoTracking().FirstOrDefaultAsync(i => i.InsoleId == id, ct);
            return insole is null ? NotFound() : Ok(ToDto(insole));
        }

        [HttpGet("{id}/telemetry")]
        public async Task<ActionResult<IEnumerable<TelemetryReadingDto>>> GetTelemetry(
            string id,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int limit,
            CancellationToken ct)
        {
            if (!await _db.Insoles.AnyAsync(i => i.InsoleId == id, ct))
                return NotFound();

            limit = limit <= 0 ? 100 : Math.Clamp(limit, 1, 1000);

            var query = _db.TelemetryReadings.AsNoTracking().Where(r => r.InsoleId == id);
            if (from.HasValue) query = query.Where(r => r.Timestamp >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Timestamp <= to.Value);

            // With `from` set, this is a "give me everything since this cursor" call (used by
            // incremental consumers like ForgeInsoleSyncService) — order oldest-first so a
            // backlog larger than `limit` is paginated forward over successive calls instead of
            // silently dropping the oldest unseen readings. Without `from`, it's a plain "show
            // me recent history" browse (e.g. from Swagger) — newest-first is the useful default.
            var readings = from.HasValue
                ? await query.OrderBy(r => r.Timestamp).Take(limit).ToListAsync(ct)
                : await query.OrderByDescending(r => r.Timestamp).Take(limit).ToListAsync(ct);

            return Ok(readings.Select(ToDto));
        }

        [HttpGet("{id}/stream")]
        public async Task StreamTelemetry(string id, CancellationToken ct)
        {
            if (!await _db.Insoles.AnyAsync(i => i.InsoleId == id, ct))
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // disable reverse-proxy buffering if this ever sits behind one

            var reader = _broadcaster.Subscribe(id, out var subscriptionId);
            try
            {
                await Response.Body.FlushAsync(ct);

                while (await reader.WaitToReadAsync(ct))
                {
                    while (reader.TryRead(out var reading))
                    {
                        var json = JsonSerializer.Serialize(ToDto(reading), JsonOptions);
                        await Response.WriteAsync($"data: {json}\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected.
            }
            finally
            {
                _broadcaster.Unsubscribe(id, subscriptionId);
            }
        }

        private static InsoleDto ToDto(Insole i) =>
            new(i.InsoleId, i.Label, i.Status.ToString(), i.Firmware, i.Side.ToString(), i.CreatedAt, i.Source.ToString());

        private static TelemetryReadingDto ToDto(TelemetryReading r) => new(
            r.Timestamp,
            new PressureMapDto(r.PressureHeel, r.PressureMidfoot, r.PressureForefoot),
            new ImuDto(new Vector3Dto(r.AccelX, r.AccelY, r.AccelZ), new Vector3Dto(r.GyroX, r.GyroY, r.GyroZ)),
            r.Cadence,
            r.GaitBalance,
            r.ContactTimeMs,
            r.FootStrike,
            r.StrideAsymmetryPct,
            r.ImpactForce,
            r.Steps,
            r.Source.ToString());

        // Public so DevicesController returns the identical status shape — two different
        // spellings of "connected device" across two endpoints would be a trap for clients.
        public static DeviceStatusDto ToStatusDto(DeviceStatus d) => new(
            d.Key,
            d.Host,
            d.WsPort,
            d.State.ToString(),
            d.InsoleId,
            d.DeviceName,
            d.Firmware,
            d.Foot,
            d.ConnectedAt,
            d.LastFrameAt,
            d.FramesReceived,
            d.ReadingsPersisted,
            d.LastError,
            d.MpuOk,
            d.MpuStalled,
            d.Steps,
            ParseFrame(d.LastFrameJson));

        // Re-parsed rather than stored as JsonElement: JsonDocument owns pooled memory that
        // must be disposed, and holding one indefinitely on a long-lived status object would
        // leak. Clone() detaches the element from the document before it is released.
        private static JsonElement? ParseFrame(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
            catch (JsonException) { return null; }
        }
    }
}
