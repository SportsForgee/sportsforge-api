using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ForgeInsole.Api.Services;
using ForgeInsole.Data;
using ForgeInsole.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeInsole.Api.Devices
{
    // One long-lived WebSocket client per physical insole.
    //
    // The API pulls from the device rather than the device pushing to the API, because the
    // shipped firmware is a WebSocket *server* (WebSocketsServer on port 81) with no outbound
    // HTTP client — so this direction needs zero firmware changes. The tradeoff is that the
    // API host must be able to reach the ESP32 on the LAN, which holds for local/on-prem
    // deployments and does not hold for a cloud-hosted API behind NAT. See the README for
    // the push-side alternative if this ever needs to run off-network.
    internal sealed class InsoleDeviceConnection
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly InsoleDeviceOptions _device;
        private readonly DeviceIngestOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TelemetryBroadcaster _broadcaster;
        private readonly DeviceStatus _status;
        private readonly ILogger _logger;

        private string? _registeredInsoleId;
        private string? _deviceName;
        private bool _authSent;

        // Peak-hold state for the persistence window — see HandleTelemetryAsync.
        private TelemetryReading? _windowPeak;
        private DateTime _windowStartedAt = DateTime.MinValue;

        public InsoleDeviceConnection(
            InsoleDeviceOptions device,
            DeviceIngestOptions options,
            IServiceScopeFactory scopeFactory,
            TelemetryBroadcaster broadcaster,
            DeviceStatus status,
            ILogger logger)
        {
            _device = device;
            _options = options;
            _scopeFactory = scopeFactory;
            _broadcaster = broadcaster;
            _status = status;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, _options.ReconnectDelaySeconds));

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ConnectAndPumpAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _status.State = DeviceConnectionState.Disconnected;
                    _status.LastError = ex.Message;

                    // Expected and frequent while a prototype is being power-cycled or carried
                    // out of WiFi range, so this is a warning with no stack trace rather than
                    // an error — the reconnect loop is the designed response, not a failure.
                    _logger.LogWarning("Insole device {Device} disconnected: {Reason}. Retrying in {Delay}s.",
                        _device.Key, ex.Message, reconnectDelay.TotalSeconds);
                }

                _status.State = DeviceConnectionState.Disconnected;
                _registeredInsoleId = null;

                try
                {
                    await Task.Delay(reconnectDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ConnectAndPumpAsync(CancellationToken ct)
        {
            using var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

            var uri = new Uri($"ws://{_device.Host}:{_device.WsPort}/");
            _status.State = DeviceConnectionState.Connecting;
            _logger.LogInformation("Connecting to insole device at {Uri}", uri);

            await socket.ConnectAsync(uri, ct);

            _status.State = DeviceConnectionState.Authenticating;
            _status.ConnectedAt = DateTime.UtcNow;
            _status.LastError = null;

            // Reset per-connection state — a reconnect may land on a different device
            // (DHCP reassignment, a swapped unit at the same address).
            _registeredInsoleId = null;
            _deviceName = null;
            _windowPeak = null;
            _windowStartedAt = DateTime.MinValue;

            // The firmware sends auth_required on connect, but it queues incoming text
            // regardless, so authenticating immediately saves a round trip. The
            // auth_required handler below deliberately does NOT re-send: doing both makes
            // the device process two auths and emit two auth_success frames per connect.
            _authSent = true;
            await SendAuthAsync(socket, ct);

            var buffer = new byte[4096];
            using var message = new MemoryStream();

            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                message.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    // The firmware streams unprompted at STREAM_HZ, so a receive that blocks
                    // this long means the link is dead in a way TCP has not surfaced yet.
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ReceiveTimeoutSeconds)));

                    try
                    {
                        result = await socket.ReceiveAsync(buffer, timeoutCts.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"no frame received for {_options.ReceiveTimeoutSeconds}s (device powered off, or off the network)");
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                await HandleMessageAsync(socket, message.GetBuffer().AsMemory(0, (int)message.Length), ct);
            }
        }

        private async Task SendAuthAsync(ClientWebSocket socket, CancellationToken ct)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(
                new { type = "auth", password = _device.Password }, JsonOptions);
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, ct);
        }

        private async Task HandleMessageAsync(ClientWebSocket socket, ReadOnlyMemory<byte> payload, CancellationToken ct)
        {
            InsoleDeviceFrame? frame;
            try
            {
                frame = JsonSerializer.Deserialize<InsoleDeviceFrame>(payload.Span, JsonOptions);
            }
            catch (JsonException ex)
            {
                // One malformed frame must not tear down a working stream.
                _logger.LogDebug(ex, "Discarded unparseable frame from {Device}", _device.Key);
                return;
            }

            if (frame?.Type is null) return;

            switch (frame.Type)
            {
                case "auth_required":
                    // Normally already handled by the eager auth on connect; this covers a
                    // device that asks again (e.g. after its own re-init).
                    if (_authSent) return;
                    _authSent = true;
                    await SendAuthAsync(socket, ct);
                    return;

                case "auth_success":
                    _status.State = DeviceConnectionState.Streaming;
                    // Only auth_success carries deviceName — sensor_data frames don't — so it
                    // has to be held here for the insole registration that follows.
                    _deviceName = frame.DeviceName;
                    _status.DeviceName = frame.DeviceName;
                    _status.Firmware = frame.Fw;
                    _logger.LogInformation("Authenticated with insole device {Device} ({Name}, fw {Firmware})",
                        _device.Key, frame.DeviceName, frame.Fw);
                    return;

                case "auth_failed":
                    // Retrying the same rejected password would just loop. The message is
                    // read by whoever typed it in the app, so it names the thing they can
                    // fix rather than a config key they have never seen.
                    throw new InvalidOperationException("Wrong device password.");

                case "sensor_data":
                    // Hand the untouched payload through so the live view shows exactly what
                    // the device said, not this service's interpretation of it.
                    await HandleTelemetryAsync(frame, payload, ct);
                    return;
            }
        }

        private async Task HandleTelemetryAsync(InsoleDeviceFrame frame, ReadOnlyMemory<byte> payload, CancellationToken ct)
        {
            var insoleId = ResolveInsoleId(frame);
            if (string.IsNullOrWhiteSpace(insoleId))
            {
                _logger.LogWarning("Frame from {Device} has no deviceId and no configured InsoleId; dropped.", _device.Key);
                return;
            }

            // The FK from TelemetryReading to Insole means the parent row has to exist before
            // any reading can be written — and a device is allowed to be plugged in without
            // having been seeded, so it registers itself on first frame.
            if (_registeredInsoleId != insoleId)
            {
                await EnsureInsoleRegisteredAsync(insoleId, frame, ct);
                _registeredInsoleId = insoleId;
            }

            _status.State = DeviceConnectionState.Streaming;
            _status.InsoleId = insoleId;
            _status.LastFrameAt = DateTime.UtcNow;
            _status.FramesReceived++;
            _status.MpuOk = frame.MpuOk;
            _status.MpuStalled = frame.MpuStalled;
            _status.Steps = frame.Steps;
            _status.Foot = frame.Foot;
            _status.LastFrameJson = Encoding.UTF8.GetString(payload.Span);

            var reading = DeviceFrameMapper.ToReading(frame, insoleId, _options);

            // Every frame reaches live SSE subscribers at the full 10 Hz; only the DB write is
            // throttled, so /stream stays as responsive as the hardware allows.
            _broadcaster.Publish(reading);

            if (_options.PersistIntervalMs <= 0)
            {
                await PersistAsync(reading, ct);
                return;
            }

            // Peak-hold rather than "whatever frame arrives on the tick". A fixed-period
            // sample of a 10 Hz gait stream aliases against the step cycle: with contact
            // occupying well under half of each stride, most tick-aligned samples land in
            // swing phase and the persisted history reads as a near-unloaded foot — the
            // loaded part of the step, which is the whole point of the measurement, gets
            // dropped. Keeping the most-loaded frame per window means the stored row is
            // always a real frame the device sent, and always the one that carries the
            // stance-phase pressure distribution and strike classification.
            var now = DateTime.UtcNow;
            if (_windowStartedAt == DateTime.MinValue) _windowStartedAt = now;

            if (_windowPeak is null || TotalPressure(reading) > TotalPressure(_windowPeak))
                _windowPeak = reading;

            if ((now - _windowStartedAt).TotalMilliseconds < _options.PersistIntervalMs) return;

            var peak = _windowPeak;
            _windowPeak = null;
            _windowStartedAt = now;

            if (peak is not null) await PersistAsync(peak, ct);
        }

        private static double TotalPressure(TelemetryReading r) =>
            r.PressureHeel + r.PressureMidfoot + r.PressureForefoot;

        private async Task PersistAsync(TelemetryReading reading, CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();
            db.TelemetryReadings.Add(reading);
            await db.SaveChangesAsync(ct);
            _status.ReadingsPersisted++;
        }

        private string ResolveInsoleId(InsoleDeviceFrame frame) =>
            !string.IsNullOrWhiteSpace(_device.InsoleId) ? _device.InsoleId!
            : frame.DeviceId ?? "";

        private async Task EnsureInsoleRegisteredAsync(string insoleId, InsoleDeviceFrame frame, CancellationToken ct)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();

            var side = _device.Side
                ?? (string.Equals(frame.Foot, "R", StringComparison.OrdinalIgnoreCase) ? InsoleSide.R : InsoleSide.L);
            var label = _device.Label ?? _deviceName ?? frame.DeviceName ?? insoleId;
            var firmware = frame.Fw ?? "";

            var existing = await db.Insoles.FirstOrDefaultAsync(i => i.InsoleId == insoleId, ct);
            if (existing is null)
            {
                db.Insoles.Add(new Insole
                {
                    InsoleId = insoleId,
                    Label = label,
                    Firmware = firmware,
                    Side = side,
                    Status = InsoleStatus.Active,
                    Source = InsoleSource.Device,
                    CreatedAt = DateTime.UtcNow,
                });
                _logger.LogInformation("Registered new device-backed insole {InsoleId} ({Label}, {Side}, fw {Firmware})",
                    insoleId, label, side, firmware);
            }
            else
            {
                // Marking an existing row as Device is what stops InsoleTelemetryHostedService
                // from generating fake readings for the same id in parallel with the real ones.
                existing.Source = InsoleSource.Device;
                existing.Status = InsoleStatus.Active;
                existing.Side = side;
                if (!string.IsNullOrWhiteSpace(firmware)) existing.Firmware = firmware;
                if (_device.Label is not null) existing.Label = _device.Label;
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
