namespace ForgeInsole.Api.Devices
{
    // Wire shape of the ESP32 firmware's WebSocket messages. Property names mirror
    // buildTelemetryJson() in the firmware sketch; binding is case-insensitive
    // (JsonSerializerDefaults.Web), so the camelCase wire names map onto these PascalCase
    // properties without per-property attributes.
    //
    // Plain settable properties rather than records on purpose: every field is optional, and
    // a partially-populated frame from an older or hand-rolled firmware build should degrade
    // to zeros rather than fail to bind and kill the stream.

    public class DeviceZones
    {
        public double Heel { get; set; }
        public double Toe { get; set; }
        public double Left { get; set; }
        public double Right { get; set; }
        public double Total { get; set; }
    }

    public class DeviceImu
    {
        public double Ax { get; set; }
        public double Ay { get; set; }
        public double Az { get; set; }
        public double Gx { get; set; }
        public double Gy { get; set; }
        public double Gz { get; set; }
        public double TempC { get; set; }
    }

    public class InsoleDeviceFrame
    {
        // "sensor_data" | "auth_required" | "auth_success" | "auth_failed"
        public string? Type { get; set; }

        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? Foot { get; set; }
        public string? Pair { get; set; }
        public string? Fw { get; set; }

        public long UptimeSec { get; set; }
        public long SessionSec { get; set; }
        public long Seq { get; set; }

        public bool MpuOk { get; set; }
        public bool MpuStalled { get; set; }

        public DeviceZones? Pressure { get; set; }
        public DeviceZones? Raw { get; set; }
        public DeviceZones? Peak { get; set; }
        public DeviceImu? Imu { get; set; }

        public int Steps { get; set; }
        public double CadenceSpm { get; set; }
        public double AvgContactMs { get; set; }
        public string? FootStrike { get; set; }
        public double BalanceMlPct { get; set; }
        public double BalanceFrPct { get; set; }
        public bool Contact { get; set; }

        // millis() since the ESP32 booted — NOT a wall clock. The device has no RTC, so
        // TelemetryReading.Timestamp is stamped server-side on arrival instead.
        public long Timestamp { get; set; }
    }
}
