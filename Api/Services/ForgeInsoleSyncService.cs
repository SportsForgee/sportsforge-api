using Api.Data;
using Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    // Bridges the ForgeInsole partner API (backend/ForgeInsole) into the existing
    // insole pipeline: for every ForgeInsole insole that's been paired to an athlete via the
    // normal POST /api/devices/pair flow (Type="Insole", SerialNumber="forge-insole-01" etc.),
    // poll ForgeInsole.Api for new readings and feed them through IHardwareTelemetryService —
    // the same path the JS simulator and real hardware use. So the dashboards, VitalsHub, and
    // /api/insole/* endpoints don't need to know or care that this data came from ForgeInsole.
    //
    // ForgeInsole.Api itself is untouched by this — SportsForge just consumes its public
    // contract like any other partner would, over HTTP with the X-Api-Key header.
    //
    // Disabled by default (ForgeInsole:Enabled=false) until you've paired at least one device.
    public class ForgeInsoleSyncService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IConfiguration _config;
        private readonly ILogger<ForgeInsoleSyncService> _logger;

        // Per-insole "synced up to" cursor, kept in memory — good enough for a dev/demo bridge.
        private readonly Dictionary<string, DateTime> _lastSynced = new();

        public ForgeInsoleSyncService(IServiceProvider services, IConfiguration config, ILogger<ForgeInsoleSyncService> logger)
        {
            _services = services;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.GetValue("ForgeInsole:Enabled", false))
            {
                _logger.LogInformation("ForgeInsoleSyncService disabled (ForgeInsole:Enabled=false).");
                return;
            }

            var intervalSeconds = _config.GetValue("ForgeInsole:PollIntervalSeconds", 5);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ForgeInsoleSyncService poll cycle failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }

        private async Task PollOnceAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var forgeInsole = scope.ServiceProvider.GetRequiredService<IForgeInsoleClient>();
            var telemetry = scope.ServiceProvider.GetRequiredService<IHardwareTelemetryService>();

            var insoles = await forgeInsole.GetInsolesAsync(ct);

            foreach (var insole in insoles)
            {
                // Only insoles someone has actually paired to an athlete (POST /api/devices/pair,
                // Type="Insole", SerialNumber=insole.InsoleId) get pulled — unpaired ForgeInsole
                // devices stay invisible to SportsForge, same as any other unpaired hardware.
                var device = await db.Devices
                    .FirstOrDefaultAsync(d => d.Type == "Insole" && d.SerialNumber == insole.InsoleId, ct);
                if (device == null) continue;

                var since = _lastSynced.TryGetValue(insole.InsoleId, out var last) ? last : DateTime.UtcNow.AddMinutes(-5);
                var readings = await forgeInsole.GetTelemetryAsync(insole.InsoleId, since, ct);
                if (readings.Count == 0) continue;

                await telemetry.IngestInsoleReadingsAsync(new InsoleIngestRequest
                {
                    DeviceSerial = insole.InsoleId,
                    AthleteId = device.AthleteId,
                    Source = "Live",
                    Readings = readings.Select(r => new InsoleReadingInput
                    {
                        Timestamp = r.Timestamp,
                        Foot = insole.Side, // ForgeInsole readings don't carry per-reading foot — the insole itself is fixed L/R
                        PressureMap = new { heel = r.Pressure.Heel, midfoot = r.Pressure.Midfoot, forefoot = r.Pressure.Forefoot },
                        Cadence = r.Cadence,
                        GroundContactMs = r.ContactTimeMs,
                        FootStrike = r.FootStrike,
                        StrideAsymmetryPct = r.StrideAsymmetryPct,
                        ImpactForce = r.ImpactForce,
                        BalanceScore = r.GaitBalance,
                    }).ToList(),
                });

                _lastSynced[insole.InsoleId] = readings.Max(r => r.Timestamp).AddTicks(1);
            }
        }
    }
}
