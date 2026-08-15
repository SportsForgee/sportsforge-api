namespace ForgeInsole.Api.Dtos
{
    public record InsoleDto(
        string InsoleId,
        string Label,
        string Status,
        string Firmware,
        string Side,
        DateTime CreatedAt);

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
        double ImpactForce);
}
