using System.Collections.Concurrent;

namespace ForgeInsole.Api.Devices
{
    public enum DeviceConnectionState
    {
        Disabled,
        Connecting,
        Authenticating,
        Streaming,
        Disconnected,
    }

    // Live per-device connection state. Without this, a device that is powered off or on the
    // wrong WiFi is indistinguishable from one that is connected but standing still — both
    // just look like "no new readings" from the telemetry endpoints.
    public class DeviceStatus
    {
        public required string Key { get; init; }
        public required string Host { get; init; }
        public required int WsPort { get; init; }

        public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;
        public string? InsoleId { get; set; }
        public string? DeviceName { get; set; }
        public string? Firmware { get; set; }
        public string? Foot { get; set; }

        public DateTime? ConnectedAt { get; set; }
        public DateTime? LastFrameAt { get; set; }
        public long FramesReceived { get; set; }
        public long ReadingsPersisted { get; set; }
        public string? LastError { get; set; }

        // Surfaced because the firmware's own self-diagnosis is the fastest way to explain a
        // stream of flat-zero IMU values (browned-out MPU6050 that reset into sleep mode).
        public bool? MpuOk { get; set; }
        public bool? MpuStalled { get; set; }
        public int? Steps { get; set; }

        // The most recent frame exactly as the firmware sent it, kept in memory only.
        //
        // Every field the device produces — all four pressure zones, raw pre-baseline counts,
        // peaks, both balance axes, IMU in the device's own units, temperature, contact flag,
        // sequence — survives here without a schema change per firmware revision. It is
        // deliberately not persisted: at 4 writes/second this would add hundreds of MB a day
        // of mostly-redundant JSON, and it answers a "what is the device doing right now"
        // question that history cannot.
        public string? LastFrameJson { get; set; }
    }

    public class DeviceRegistry
    {
        private readonly ConcurrentDictionary<string, DeviceStatus> _devices = new();

        public DeviceStatus GetOrAdd(InsoleDeviceOptions device) =>
            _devices.GetOrAdd(device.Key, _ => new DeviceStatus
            {
                Key = device.Key,
                Host = device.Host,
                WsPort = device.WsPort,
                InsoleId = string.IsNullOrWhiteSpace(device.InsoleId) ? null : device.InsoleId,
            });

        public IReadOnlyCollection<DeviceStatus> All() => _devices.Values.ToList();

        // Dropped entirely rather than left as a Disconnected row: after an explicit
        // disconnect the device is no longer something this service tracks, and a lingering
        // stale entry would keep it on the client's connected list.
        public void Remove(string key) => _devices.TryRemove(key, out _);

        public DeviceStatus? ForInsole(string insoleId) =>
            _devices.Values.FirstOrDefault(d =>
                string.Equals(d.InsoleId, insoleId, StringComparison.OrdinalIgnoreCase));
    }
}
