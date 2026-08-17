using Api.Models;
using Api.Models.Dtos;

namespace Api.Services
{
    // Single place that persists a device reading and broadcasts it over /hubs/vitals.
    // Used by InsoleController/WearableController (simulator + real device/mobile path)
    // and, later, by KhoiSyncService (real Khoi Cloud path) — so both paths share one
    // code path and behave identically to downstream consumers.
    public interface IHardwareTelemetryService
    {
        Task<Device> GetOrRegisterDeviceAsync(string athleteId, string type, string serialNumber, string? firmwareVersion = null);

        Task IngestInsoleReadingsAsync(InsoleIngestRequest request);
        Task IngestWearableReadingsAsync(WearableIngestRequest request);

        Task<List<DeviceDto>> GetDevicesForAthleteAsync(string athleteId);

        Task<InsoleReadingDto?>   GetLatestInsoleReadingAsync(string athleteId);
        Task<InsoleTrendDto>      GetInsoleTrendAsync(string athleteId, int days);
        Task<WearableReadingDto?> GetLatestWearableReadingAsync(string athleteId);

        Task<InsoleSummaryDto>   GetInsoleSummaryAsync(string athleteId, TimeSpan window);
        Task<WearableSummaryDto> GetWearableSummaryAsync(string athleteId, TimeSpan window);
    }
}
