using ForgeInsole.Data;
using ForgeInsole.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeInsole.Api.Services
{
    // Continuously generates telemetry for every Active insole, persists it, and publishes
    // each reading to TelemetryBroadcaster for any live SSE subscribers.
    public class InsoleTelemetryHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TelemetryBroadcaster _broadcaster;
        private readonly ILogger<InsoleTelemetryHostedService> _logger;
        private readonly int _intervalMs;
        private readonly bool _enabled;
        private readonly Dictionary<string, InsoleGeneratorState> _state = new();

        public InsoleTelemetryHostedService(
            IServiceScopeFactory scopeFactory,
            TelemetryBroadcaster broadcaster,
            IConfiguration configuration,
            ILogger<InsoleTelemetryHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _broadcaster = broadcaster;
            _logger = logger;
            _intervalMs = configuration.GetValue<int?>("ForgeInsole:GenerationIntervalMs") ?? 2000;
            _enabled = configuration.GetValue<bool?>("ForgeInsole:SimulationEnabled") ?? true;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation(
                    "Forge Insole telemetry generator disabled (ForgeInsole:SimulationEnabled=false) — " +
                    "only real device readings will be recorded.");
                return;
            }

            var session = new SimulationSession { StartedAt = DateTime.UtcNow };
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();
                db.SimulationSessions.Add(session);
                await db.SaveChangesAsync(stoppingToken);
            }

            _logger.LogInformation(
                "Forge Insole telemetry generator started (session {SessionId}, interval {IntervalMs}ms)",
                session.Id, _intervalMs);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();

                        // Source == Simulated is the guard that keeps generated readings from
                        // interleaving with real ones for an insole a physical device is
                        // streaming into (DeviceIngestHostedService flips the row to Device on
                        // its first frame).
                        var activeInsoles = await db.Insoles
                            .AsNoTracking()
                            .Where(i => i.Status == InsoleStatus.Active && i.Source == InsoleSource.Simulated)
                            .ToListAsync(stoppingToken);

                        foreach (var insole in activeInsoles)
                        {
                            if (!_state.TryGetValue(insole.InsoleId, out var state))
                                _state[insole.InsoleId] = state = new InsoleGeneratorState();

                            var reading = TelemetryGenerator.NextReading(insole.InsoleId, insole.Side, state);
                            reading.SimulationSessionId = session.Id;
                            db.TelemetryReadings.Add(reading);
                            _broadcaster.Publish(reading);
                        }

                        await db.SaveChangesAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // A transient DB hiccup (e.g. a command timeout under load) should cost
                        // this one tick, not take down the whole host — BackgroundService's
                        // default HostOptions.BackgroundServiceExceptionBehavior is StopHost,
                        // so an unhandled exception here would otherwise crash ForgeInsole.Api
                        // entirely (observed in practice: a SaveChangesAsync timeout during a
                        // multi-service simultaneous startup took the whole process down).
                        _logger.LogWarning(ex, "Telemetry generation tick failed; will retry next tick.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
            finally
            {
                session.EndedAt = DateTime.UtcNow;
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();
                db.SimulationSessions.Attach(session);
                db.Entry(session).Property(s => s.EndedAt).IsModified = true;
                await db.SaveChangesAsync(CancellationToken.None);

                _logger.LogInformation("Forge Insole telemetry generator stopped (session {SessionId})", session.Id);
            }
        }
    }
}
