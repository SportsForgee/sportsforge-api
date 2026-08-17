using System.Collections.Concurrent;
using ForgeInsole.Api.Services;
using Microsoft.Extensions.Options;

namespace ForgeInsole.Api.Devices
{
    // Owns the set of live device connections at runtime.
    //
    // Connections used to come only from appsettings, which meant the device's address and
    // password had to be known at deploy time and the only way to stop streaming was to
    // restart the service. This lets a client discover a device, connect with a password it
    // supplies, and disconnect again — none of which touches configuration.
    public class InsoleDeviceManager
    {
        private sealed class Running
        {
            public required InsoleDeviceOptions Device { get; init; }
            public required CancellationTokenSource Cts { get; init; }
            public required Task Task { get; init; }
        }

        private readonly ConcurrentDictionary<string, Running> _running = new();

        private readonly DeviceIngestOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TelemetryBroadcaster _broadcaster;
        private readonly DeviceRegistry _registry;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<InsoleDeviceManager> _logger;

        public InsoleDeviceManager(
            IOptions<DeviceIngestOptions> options,
            IServiceScopeFactory scopeFactory,
            TelemetryBroadcaster broadcaster,
            DeviceRegistry registry,
            ILoggerFactory loggerFactory,
            ILogger<InsoleDeviceManager> logger)
        {
            _options = options.Value;
            _scopeFactory = scopeFactory;
            _broadcaster = broadcaster;
            _registry = registry;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public DeviceIngestOptions Options => _options;

        public bool IsConnected(string key) => _running.ContainsKey(key);

        public IReadOnlyCollection<DeviceStatus> Statuses() => _registry.All();

        /// Starts streaming from a device. Reconnecting an already-connected key is a no-op
        /// so a double-tap in the UI can't spawn two sockets to the same board.
        public bool Connect(InsoleDeviceOptions device)
        {
            if (_running.ContainsKey(device.Key)) return false;

            var status = _registry.GetOrAdd(device);
            status.State = DeviceConnectionState.Connecting;
            status.LastError = null;

            var cts = new CancellationTokenSource();
            var connection = new InsoleDeviceConnection(
                device, _options, _scopeFactory, _broadcaster, status,
                _loggerFactory.CreateLogger($"ForgeInsole.Devices.{device.Key}"));

            var task = Task.Run(() => connection.RunAsync(cts.Token), cts.Token);
            _running[device.Key] = new Running { Device = device, Cts = cts, Task = task };

            _logger.LogInformation("Connecting to insole device {Key}", device.Key);
            return true;
        }

        public async Task<bool> DisconnectAsync(string key)
        {
            if (!_running.TryRemove(key, out var running)) return false;

            running.Cts.Cancel();
            try
            {
                // The receive loop can be parked on a socket read, so give it a moment rather
                // than returning while the connection is still writing telemetry.
                await running.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Cancellation or timeout — either way the token is cancelled and the loop
                // will exit on its own; nothing useful to report to the caller.
            }
            finally
            {
                running.Cts.Dispose();
            }

            _registry.Remove(key);
            _logger.LogInformation("Disconnected insole device {Key}", key);
            return true;
        }

        public async Task DisconnectAllAsync()
        {
            foreach (var key in _running.Keys.ToList()) await DisconnectAsync(key);
        }
    }
}
