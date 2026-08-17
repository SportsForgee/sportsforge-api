using ForgeInsole.Data.Entities;

namespace ForgeInsole.Api.Devices
{
    // Bound from ForgeInsole:DeviceIngest. With no Devices configured the whole ingest
    // subsystem is inert, so the default appsettings.json keeps the service simulator-only.
    public class DeviceIngestOptions
    {
        public const string SectionName = "ForgeInsole:DeviceIngest";

        public bool Enabled { get; set; } = true;

        // The firmware pushes at STREAM_HZ (10 Hz). Every frame is broadcast to SSE
        // subscribers, but persisting 10 rows/second/device would add ~864k rows per device
        // per day for data that is mostly redundant at that resolution, so DB writes are
        // throttled to this interval. Set to 0 to persist every frame.
        public int PersistIntervalMs { get; set; } = 1000;

        public int ReconnectDelaySeconds { get; set; } = 5;

        // The firmware streams unprompted at 10 Hz, so silence this long means the socket is
        // dead in a way TCP hasn't noticed yet (device browned out, WiFi dropped, AP moved it).
        public int ReceiveTimeoutSeconds { get; set; } = 15;

        // ESP32 ADC is 12-bit (analogReadResolution(12) in setup()), so a fully saturated
        // zone reads 4095 counts. Used to normalise the firmware's raw counts into the 0-100
        // pressure scale the rest of this API speaks.
        public int PressureFullScaleCounts { get; set; } = 4095;

        // Counts across all four zones that correspond to one body weight. This is an
        // UNCALIBRATED placeholder: FSRs are not load cells, and mapping counts to real force
        // needs a per-athlete calibration step the firmware doesn't do yet. ImpactForce on
        // device-sourced readings is therefore a relative load index, not newtons or true BW.
        public int CountsPerBodyWeight { get; set; } = 4000;

        public List<InsoleDeviceOptions> Devices { get; set; } = new();
    }

    public class InsoleDeviceOptions
    {
        // Hostname or IP. The firmware advertises mDNS (MDNS_HOST), so "forge-insole-l.local"
        // works on Windows 10+/macOS; an IP is the reliable fallback on networks where mDNS
        // is filtered (most guest/corporate WiFi).
        public string Host { get; set; } = "";

        public int WsPort { get; set; } = 81;

        // Matches DEVICE_PASSWORD in the firmware. Prototype-grade shared secret — keep the
        // real one in env/secrets (ForgeInsole__DeviceIngest__Devices__0__Password), not in
        // a committed appsettings file.
        public string Password { get; set; } = "";

        // Optional override for the id this device's readings are filed under. Empty means
        // "use whatever deviceId the firmware reports" (e.g. FRG-2026-P01), which keeps the
        // hardware as the single source of truth for its own identity.
        public string? InsoleId { get; set; }

        public string? Label { get; set; }

        // Optional override for the firmware's UNIT_FOOT ("L"/"R").
        public InsoleSide? Side { get; set; }

        public string Key => $"{Host}:{WsPort}";
    }
}
