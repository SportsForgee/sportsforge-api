using System.Net.Http.Json;
using System.Text.Json;

namespace Api.Services
{
    // Thin client for the ForgeInsole partner API (backend/ForgeInsole) — talks to it exactly
    // the way an external partner (Khoi Tech) would: HTTP + X-Api-Key, no shared code, no
    // project reference. These DTOs mirror ForgeInsole.Api's public JSON contract; keep them
    // in sync with backend/ForgeInsole/ForgeInsole.Api/Dtos/InsoleDtos.cs by hand if that ever changes.
    public record ForgeInsoleDto(string InsoleId, string Label, string Status, string Firmware, string Side, DateTime CreatedAt);

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
        double ImpactForce);

    public interface IForgeInsoleClient
    {
        Task<List<ForgeInsoleDto>> GetInsolesAsync(CancellationToken ct);
        Task<List<ForgeTelemetryReadingDto>> GetTelemetryAsync(string insoleId, DateTime from, CancellationToken ct);
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
    }
}
