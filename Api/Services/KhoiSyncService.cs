using Api.Data;
using Api.Models.Dtos;
using KhoiIntegration;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    // Polls Khoi Cloud for every paired Wearable device and feeds readings through the
    // same IHardwareTelemetryService path the khoi-wearable-sim uses, so downstream
    // (DB, VitalsHub, dashboards) never has to know whether a reading came from the
    // simulator or the real cloud.
    //
    // Disabled by default (Khoi:Enabled=false) — there is no reachable Khoi Cloud
    // endpoint in this environment yet. Flip it on once Khoi:BaseUrl/ApiKey are real.
    //
    // Known simplification: uses Device.SerialNumber as Khoi Cloud's externalAthleteId.
    // See hardware-integration/docs/khoi-api-contract.md for why this mapping is a gap,
    // not a confirmed design.
    public class KhoiSyncService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration    _config;
        private readonly ILogger<KhoiSyncService> _logger;

        public KhoiSyncService(IServiceProvider services, IConfiguration config, ILogger<KhoiSyncService> logger)
        {
            _services = services;
            _config   = config;
            _logger   = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("Khoi:Enabled", false))
            {
                _logger.LogInformation("KhoiSyncService disabled (Khoi:Enabled=false) — no Khoi Cloud endpoint configured.");
                return;
            }

            var intervalSeconds = _config.GetValue("Khoi:PollIntervalSeconds", 30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "KhoiSyncService poll cycle failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }

        private async Task PollOnceAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var khoi       = scope.ServiceProvider.GetRequiredService<IKhoiClient>();
            var telemetry  = scope.ServiceProvider.GetRequiredService<IHardwareTelemetryService>();

            var wearables = await db.Devices.Where(d => d.Type == "Wearable").ToListAsync(ct);

            foreach (var device in wearables)
            {
                var response = await khoi.GetLatestReadingsAsync(device.SerialNumber, ct);
                if (response == null || response.Readings.Count == 0) continue;

                await telemetry.IngestWearableReadingsAsync(new WearableIngestRequest
                {
                    DeviceSerial   = device.SerialNumber,
                    AthleteId      = device.AthleteId,
                    BatteryPercent = response.BatteryPercent,
                    Readings = response.Readings.Select(r => new WearableReadingInput
                    {
                        Timestamp     = r.Timestamp,
                        HeartRate     = r.HeartRate,
                        RecoveryScore = r.RecoveryScore,
                        StressLevel   = r.StressLevel,
                        SpO2          = r.SpO2,
                        HydrationPct  = r.HydrationPct,
                        StaminaPct    = r.StaminaPct,
                    }).ToList(),
                });
            }
        }
    }
}
