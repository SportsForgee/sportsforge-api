namespace ForgeInsole.Api.Dtos
{
    public record InsoleDto(
        string InsoleId,
        string Label,
        string Status,
        string Firmware,
        string Side,
        DateTime CreatedAt,
        // "Simulated" | "Device" — tells a consumer whether these readings came off real
        // hardware. Appended rather than inserted so existing positional deserializers
        // (e.g. backend/Api's ForgeInsoleDto) keep binding without changes.
        string Source);

    public record Vector3Dto(double X, double Y, double Z);

    public record ImuDto(Vector3Dto Accel, Vector3Dto Gyro);

    public record PressureMapDto(double Heel, double Midfoot, double Forefoot);

    public record TelemetryReadingDto(
        DateTime Timestamp,
        PressureMapDto Pressure,
        ImuDto Imu,
        double Cadence,
        double GaitBalance,
        double ContactTimeMs,
        string FootStrike,
        double StrideAsymmetryPct,
        double ImpactForce,
        int Steps,
        string Source);

    // Live connection state for a configured physical insole. Without this a powered-off
    // device and a stationary athlete look identical from the telemetry endpoints — both
    // are simply an absence of new readings.
    public record DeviceStatusDto(
        string Key,
        string Host,
        int WsPort,
        string State,
        string? InsoleId,
        string? DeviceName,
        string? Firmware,
        string? Foot,
        DateTime? ConnectedAt,
        DateTime? LastFrameAt,
        long FramesReceived,
        long ReadingsPersisted,
        string? LastError,
        bool? MpuOk,
        bool? MpuStalled,
        int? Steps,
        // The device's own latest frame, passed through verbatim as nested JSON so a client
        // can render every field the firmware emits — including ones this API has no typed
        // column for. Null until the first frame arrives.
        System.Text.Json.JsonElement? LastFrame);

    // Identity only — deliberately no host or port. The client picks a device by name and
    // connects by DeviceId; resolving that to an address is this service's job, so a rotating
    // DHCP lease never reaches the UI and there is no address for a user to mistype.
    public record DiscoveredInsoleDto(
        string DeviceId,
        string DeviceName,
        string Foot,
        string Firmware,
        bool MpuOk,
        bool RequiresAuth,
        bool Connected);

    public record ConnectDeviceRequest(string? DeviceId, string? Password, string? InsoleId);

    public record DisconnectDeviceRequest(string? DeviceId);
}
