using ForgeInsole.Api.Devices;
using Microsoft.Extensions.Options;

namespace ForgeInsole.Api.Services
{
    // Brings up any devices listed in configuration at boot and tears every connection down
    // on shutdown. Runtime connect/disconnect is InsoleDeviceManager's job — this only owns
    // the lifecycle around it, so a device connected from the app is never clobbered by
    // config and vice versa.
    public class DeviceIngestHostedService : BackgroundService
    {
        private readonly DeviceIngestOptions _options;
        private readonly InsoleDeviceManager _manager;
        private readonly ILogger<DeviceIngestHostedService> _logger;

        public DeviceIngestHostedService(
            IOptions<DeviceIngestOptions> options,
            InsoleDeviceManager manager,
            ILogger<DeviceIngestHostedService> logger)
        {
            _options = options.Value;
            _manager = manager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Forge Insole device ingest disabled by configuration.");
                return;
            }

            var autoStarted = 0;
            foreach (var device in _options.Devices)
            {
                if (string.IsNullOrWhiteSpace(device.Host))
                {
                    _logger.LogError("Skipping a ForgeInsole:DeviceIngest:Devices entry with no Host set.");
                    continue;
                }
                _manager.Connect(device);
                autoStarted++;
            }

            _logger.LogInformation(
                "Forge Insole device ingest ready ({Count} device(s) auto-connected from config). " +
                "Use POST /api/v1/devices/connect to add one at runtime.", autoStarted);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }

            await _manager.DisconnectAllAsync();
        }
    }
}
