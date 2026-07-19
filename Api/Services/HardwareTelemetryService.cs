using System.Text.Json;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public class HardwareTelemetryService : IHardwareTelemetryService
    {
        // A device is considered "Connected" if it has synced within this window.
        private static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(90);

        private readonly AppDbContext _db;
        private readonly IHubContext<VitalsHub> _hub;

        public HardwareTelemetryService(AppDbContext db, IHubContext<VitalsHub> hub)
        {
            _db  = db;
            _hub = hub;
        }

        public async Task<Device> GetOrRegisterDeviceAsync(string athleteId, string type, string serialNumber, string? firmwareVersion = null)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);
            if (device == null)
            {
                device = new Device
                {
                    AthleteId       = athleteId,
                    Type            = type,
                    SerialNumber    = serialNumber,
                    FirmwareVersion = firmwareVersion,
                };
                _db.Devices.Add(device);
                await _db.SaveChangesAsync();
            }
            return device;
        }

        public async Task IngestInsoleReadingsAsync(InsoleIngestRequest request)
        {
            var device = await GetOrRegisterDeviceAsync(request.AthleteId, "Insole", request.DeviceSerial);

            var entities = request.Readings.Select(r => new InsoleReading
            {
                DeviceId           = device.Id,
                AthleteId          = request.AthleteId,
                Timestamp          = r.Timestamp,
                Foot               = r.Foot,
                PressureMapJson    = r.PressureMap == null ? null : JsonSerializer.Serialize(r.PressureMap),
                Cadence            = r.Cadence,
                GroundContactMs    = r.GroundContactMs,
                FootStrike         = r.FootStrike,
                StrideAsymmetryPct = r.StrideAsymmetryPct,
                ImpactForce        = r.ImpactForce,
                BalanceScore       = r.BalanceScore,
            }).ToList();

            _db.InsoleReadings.AddRange(entities);

            await RecordSyncAsync(device, request.Source, entities.Count);
            await _db.SaveChangesAsync();

            var latest = entities.OrderByDescending(e => e.Timestamp).FirstOrDefault();
            if (latest != null)
            {
                await _hub.Clients.Group($"athlete-{request.AthleteId}")
                    .SendAsync("InsoleUpdate", ToDto(latest));
            }
        }

        public async Task IngestWearableReadingsAsync(WearableIngestRequest request)
        {
            var device = await GetOrRegisterDeviceAsync(request.AthleteId, "Wearable", request.DeviceSerial);
            if (request.BatteryPercent.HasValue) device.BatteryPercent = request.BatteryPercent;

            var entities = request.Readings.Select(r => new WearableReading
            {
                DeviceId      = device.Id,
                AthleteId     = request.AthleteId,
                Timestamp     = r.Timestamp,
                HeartRate     = r.HeartRate,
                RecoveryScore = r.RecoveryScore,
                StressLevel   = r.StressLevel,
                SpO2          = r.SpO2,
                HydrationPct  = r.HydrationPct,
                StaminaPct    = r.StaminaPct,
            }).ToList();

            _db.WearableReadings.AddRange(entities);

            await RecordSyncAsync(device, "Live", entities.Count);
            await _db.SaveChangesAsync();

            var latest = entities.OrderByDescending(e => e.Timestamp).FirstOrDefault();
            if (latest != null)
            {
                await _hub.Clients.Group($"athlete-{request.AthleteId}")
                    .SendAsync("WearableUpdate", ToDto(latest));
            }
        }

        private async Task RecordSyncAsync(Device device, string source, int packetCount)
        {
            device.LastSyncAt = DateTime.UtcNow;
            device.Status     = "Connected";

            _db.SyncSessions.Add(new SyncSession
            {
                DeviceId    = device.Id,
                StartedAt   = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                PacketCount = packetCount,
                Source      = source,
            });

            await Task.CompletedTask;
        }

        public async Task<List<DeviceDto>> GetDevicesForAthleteAsync(string athleteId)
        {
            var devices = await _db.Devices
                .Where(d => d.AthleteId == athleteId)
                .ToListAsync();

            return devices.Select(ToDto).ToList();
        }

        public async Task<InsoleReadingDto?> GetLatestInsoleReadingAsync(string athleteId)
        {
            var reading = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            return reading == null ? null : ToDto(reading);
        }

        public async Task<WearableReadingDto?> GetLatestWearableReadingAsync(string athleteId)
        {
            var reading = await _db.WearableReadings
                .Where(r => r.AthleteId == athleteId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            return reading == null ? null : ToDto(reading);
        }

        public async Task<InsoleSummaryDto> GetInsoleSummaryAsync(string athleteId, TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var readings = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId && r.Timestamp >= since)
                .ToListAsync();

            // Forge Insole hasn't shipped — Top Speed has no insole/wearable source at all,
            // and Balance falls back to the latest analyzed video when no insole readings
            // exist yet. Once real insole data exists it always wins for balance.
            var latestVideoResult = await _db.VideoUploads
                .Where(v => v.AthleteId == athleteId)
                .Join(_db.VideoAnalysisResults, v => v.Id, r => r.VideoUploadId, (v, r) => r)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            var topSpeed = latestVideoResult?.TopSpeedKmh;
            var topSpeedSource = topSpeed.HasValue ? "video" : null;

            if (readings.Count == 0)
            {
                return new InsoleSummaryDto
                {
                    ReadingCount        = 0,
                    AvgBalanceScore     = latestVideoResult?.GaitBalanceScore,
                    BalanceScoreSource  = latestVideoResult?.GaitBalanceScore.HasValue == true ? "video" : null,
                    TopSpeedKmh         = topSpeed,
                    TopSpeedSource      = topSpeedSource,
                };
            }

            return new InsoleSummaryDto
            {
                AvgCadence           = readings.Where(r => r.Cadence.HasValue).Select(r => r.Cadence!.Value).DefaultIfEmpty().Average(),
                AvgBalanceScore      = readings.Where(r => r.BalanceScore.HasValue).Select(r => r.BalanceScore!.Value).DefaultIfEmpty().Average(),
                BalanceScoreSource   = "insole",
                AvgStrideAsymmetryPct= readings.Where(r => r.StrideAsymmetryPct.HasValue).Select(r => r.StrideAsymmetryPct!.Value).DefaultIfEmpty().Average(),
                AvgImpactForce       = readings.Where(r => r.ImpactForce.HasValue).Select(r => r.ImpactForce!.Value).DefaultIfEmpty().Average(),
                ReadingCount         = readings.Count,
                LastReadingAt        = readings.Max(r => r.Timestamp),
                TopSpeedKmh          = topSpeed,
                TopSpeedSource       = topSpeedSource,
            };
        }

        public async Task<WearableSummaryDto> GetWearableSummaryAsync(string athleteId, TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var readings = await _db.WearableReadings
                .Where(r => r.AthleteId == athleteId && r.Timestamp >= since)
                .ToListAsync();

            if (readings.Count == 0)
                return new WearableSummaryDto { ReadingCount = 0 };

            return new WearableSummaryDto
            {
                AvgHeartRate     = readings.Where(r => r.HeartRate.HasValue).Select(r => (double)r.HeartRate!.Value).DefaultIfEmpty().Average(),
                AvgRecoveryScore = readings.Where(r => r.RecoveryScore.HasValue).Select(r => r.RecoveryScore!.Value).DefaultIfEmpty().Average(),
                AvgStressLevel   = readings.Where(r => r.StressLevel.HasValue).Select(r => r.StressLevel!.Value).DefaultIfEmpty().Average(),
                AvgHydrationPct  = readings.Where(r => r.HydrationPct.HasValue).Select(r => r.HydrationPct!.Value).DefaultIfEmpty().Average(),
                AvgStaminaPct    = readings.Where(r => r.StaminaPct.HasValue).Select(r => r.StaminaPct!.Value).DefaultIfEmpty().Average(),
                ReadingCount     = readings.Count,
                LastReadingAt    = readings.Max(r => r.Timestamp),
            };
        }

        private static DeviceDto ToDto(Device d) => new()
        {
            Id              = d.Id,
            Type            = d.Type,
            SerialNumber    = d.SerialNumber,
            FirmwareVersion = d.FirmwareVersion,
            PairedAt        = d.PairedAt,
            LastSyncAt      = d.LastSyncAt,
            BatteryPercent  = d.BatteryPercent,
            Status          = d.LastSyncAt.HasValue && DateTime.UtcNow - d.LastSyncAt.Value <= ConnectedWindow
                                ? "Connected"
                                : "Disconnected",
        };

        private static InsoleReadingDto ToDto(InsoleReading r) => new()
        {
            Timestamp          = r.Timestamp,
            Foot               = r.Foot,
            Cadence            = r.Cadence,
            GroundContactMs    = r.GroundContactMs,
            FootStrike         = r.FootStrike,
            StrideAsymmetryPct = r.StrideAsymmetryPct,
            ImpactForce        = r.ImpactForce,
            BalanceScore       = r.BalanceScore,
        };

        private static WearableReadingDto ToDto(WearableReading r) => new()
        {
            Timestamp     = r.Timestamp,
            HeartRate     = r.HeartRate,
            RecoveryScore = r.RecoveryScore,
            StressLevel   = r.StressLevel,
            SpO2          = r.SpO2,
            HydrationPct  = r.HydrationPct,
            StaminaPct    = r.StaminaPct,
        };
    }
}
