using System.Net.Http.Json;
using System.Text.Json;

namespace Api.Services
{
    // Thin client for the ForgeInsole partner API (backend/ForgeInsole) — talks to it exactly
    // the way an external partner (Khoi Tech) would: HTTP + X-Api-Key, no shared code, no
    // project reference. These DTOs mirror ForgeInsole.Api's public JSON contract; keep them
    // in sync with backend/ForgeInsole/ForgeInsole.Api/Dtos/InsoleDtos.cs by hand if that ever changes.
    // Source is "Simulated" or "Device" — ForgeInsole.Api sets the latter for insoles whose
    // readings come off real ESP32 hardware rather than its generator.
    public record ForgeInsoleDto(string InsoleId, string Label, string Status, string Firmware, string Side, DateTime CreatedAt, string Source = "Simulated");

    public record ForgeVector3Dto(double X, double Y, double Z);
    public record ForgeImuDto(ForgeVector3Dto Accel, ForgeVector3Dto Gyro);
    public record ForgePressureMapDto(double Heel, double Midfoot, double Forefoot);

    public record ForgeTelemetryReadingDto(
        DateTime Timestamp,
        ForgePressureMapDto Pressure,
        ForgeImuDto Imu,
        double Cadence,
        double GaitBalance,
        double ContactTimeMs,
        string FootStrike,
        double StrideAsymmetryPct,
        double ImpactForce,
        // Defaulted so this keeps binding against a ForgeInsole.Api deployment that predates
        // the device-ingest fields.
        int Steps = 0,
        string Source = "Simulated");

    // Discovery/connection control, mirroring ForgeInsole.Api's /api/v1/devices contract.
    // No host or port appears anywhere here — a device is identified by the id the firmware
    // reports, and addresses stay inside ForgeInsole.Api.
    public record ForgeDiscoveredInsoleDto(
        string DeviceId, string DeviceName, string Foot, string Firmware,
        bool MpuOk, bool RequiresAuth, bool Connected);

    public record ForgeDeviceStatusDto(
        string Key, string Host, int WsPort, string State, string? InsoleId, string? DeviceName,
        string? Firmware, string? Foot, DateTime? ConnectedAt, DateTime? LastFrameAt,
        long FramesReceived, long ReadingsPersisted, string? LastError,
        bool? MpuOk, bool? MpuStalled, int? Steps,
        // Passed straight through to the app so the live sensor view can render every field
        // the firmware sends without this API needing to know their names.
        JsonElement? LastFrame);

    public interface IForgeInsoleClient
    {
        Task<List<ForgeInsoleDto>> GetInsolesAsync(CancellationToken ct);
        Task<List<ForgeTelemetryReadingDto>> GetTelemetryAsync(string insoleId, DateTime from, CancellationToken ct);

        Task<List<ForgeDiscoveredInsoleDto>> DiscoverAsync(CancellationToken ct);
        Task<(bool ok, string? error)> ConnectAsync(string deviceId, string password, CancellationToken ct);
        Task<bool> DisconnectAsync(string? deviceId, CancellationToken ct);
        Task<List<ForgeDeviceStatusDto>> GetConnectedAsync(CancellationToken ct);
    }

    public class ForgeInsoleClient : IForgeInsoleClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public ForgeInsoleClient(HttpClient http) => _http = http;

        public async Task<List<ForgeInsoleDto>> GetInsolesAsync(CancellationToken ct)
        {
            var result = await _http.GetFromJsonAsync<List<ForgeInsoleDto>>("api/v1/insoles", JsonOptions, ct);
            return result ?? new List<ForgeInsoleDto>();
        }

        public async Task<List<ForgeTelemetryReadingDto>> GetTelemetryAsync(string insoleId, DateTime from, CancellationToken ct)
        {
            var url = $"api/v1/insoles/{Uri.EscapeDataString(insoleId)}/telemetry?from={Uri.EscapeDataString(from.ToString("o"))}&limit=200";
            var result = await _http.GetFromJsonAsync<List<ForgeTelemetryReadingDto>>(url, JsonOptions, ct);
            return result ?? new List<ForgeTelemetryReadingDto>();
        }

        public async Task<List<ForgeDiscoveredInsoleDto>> DiscoverAsync(CancellationToken ct)
        {
            var result = await _http.GetFromJsonAsync<List<ForgeDiscoveredInsoleDto>>("api/v1/devices/discover", JsonOptions, ct);
            return result ?? new List<ForgeDiscoveredInsoleDto>();
        }

        public async Task<(bool ok, string? error)> ConnectAsync(string deviceId, string password, CancellationToken ct)
        {
            var res = await _http.PostAsJsonAsync("api/v1/devices/connect",
                new { deviceId, password }, JsonOptions, ct);

            if (res.IsSuccessStatusCode) return (true, null);

            // Surface the device's own reason (wrong password, not answering) rather than a
            // bare status code — it's the only actionable part for whoever tapped Connect.
            try
            {
                var problem = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(JsonOptions, ct);
                var message = problem != null && problem.TryGetValue("message", out var m) ? m?.ToString() : null;
                return (false, message ?? $"Connect failed ({(int)res.StatusCode}).");
            }
            catch
            {
                return (false, $"Connect failed ({(int)res.StatusCode}).");
            }
        }

        public async Task<bool> DisconnectAsync(string? deviceId, CancellationToken ct)
        {
            var res = await _http.PostAsJsonAsync("api/v1/devices/disconnect", new { deviceId }, JsonOptions, ct);
            return res.IsSuccessStatusCode;
        }

        public async Task<List<ForgeDeviceStatusDto>> GetConnectedAsync(CancellationToken ct)
        {
            var result = await _http.GetFromJsonAsync<List<ForgeDeviceStatusDto>>("api/v1/devices", JsonOptions, ct);
            return result ?? new List<ForgeDeviceStatusDto>();
        }
    }
}
