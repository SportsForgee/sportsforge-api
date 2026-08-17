namespace Api.Models.Dtos
{
    public class InsoleReadingInput
    {
        public DateTime Timestamp          { get; set; }
        public string   Foot               { get; set; } = "";
        public object?  PressureMap        { get; set; }
        public double?  Cadence            { get; set; }
        public double?  GroundContactMs    { get; set; }
        public string?  FootStrike         { get; set; }
        public double?  StrideAsymmetryPct { get; set; }
        public double?  ImpactForce        { get; set; }
        public double?  BalanceScore       { get; set; }
        public int?     Steps              { get; set; }
        public double?  AccelX             { get; set; }
        public double?  AccelY             { get; set; }
        public double?  AccelZ             { get; set; }
        public double?  GyroX              { get; set; }
        public double?  GyroY              { get; set; }
        public double?  GyroZ              { get; set; }
    }

    public class InsoleIngestRequest
    {
        public string   DeviceSerial { get; set; } = "";
        public string   AthleteId    { get; set; } = "";
        public string   Source       { get; set; } = "Live"; // Live | OfflineSync
        public List<InsoleReadingInput> Readings { get; set; } = new();
    }

    // Pressure per zone, 0-100. PressureMapJson on the entity is deliberately free-form
    // (the sensor layout is still moving), but the shape the Forge Insole actually sends is
    // stable enough for clients to bind to, so it's typed here rather than leaking raw JSON
    // into the UI.
    public class InsolePressureDto
    {
        public double Heel     { get; set; }
        public double Midfoot  { get; set; }
        public double Forefoot { get; set; }
    }

    public class InsoleReadingDto
    {
        public DateTime Timestamp          { get; set; }
        public string   Foot               { get; set; } = "";
        public double?  Cadence            { get; set; }
        public double?  GroundContactMs    { get; set; }
        public string?  FootStrike         { get; set; }
        public double?  StrideAsymmetryPct { get; set; }
        public double?  ImpactForce        { get; set; }
        public double?  BalanceScore       { get; set; }
        public int?     Steps              { get; set; }

        // Was previously stored but never exposed — the app had no way to render a pressure
        // map even though every reading carried one.
        public InsolePressureDto? Pressure { get; set; }

        // MPU6050 motion. Null when the reading carries no IMU sample at all, so a client can
        // tell "sensor absent" apart from "sensor reading zero" — a browned-out MPU6050 streams
        // genuine zeros, and those two states need different messages.
        public InsoleImuDto? Imu { get; set; }
    }

    public class Vector3Dto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class InsoleImuDto
    {
        public Vector3Dto Accel { get; set; } = new();
        public Vector3Dto Gyro  { get; set; } = new();
    }

    // One day of stored insole readings, collapsed to averages. Every chart series on the
    // performance screen is built from these — nothing on that screen is generated.
    public class InsoleTrendPointDto
    {
        public DateTime Date            { get; set; }
        public string   Label           { get; set; } = "";
        public double?  GaitBalance     { get; set; }
        public double?  Cadence         { get; set; }
        public double?  ContactTimeMs   { get; set; }
        public double?  AsymmetryPct    { get; set; }
        public double?  ImpactForce     { get; set; }
        public double?  PressureHeel    { get; set; }
        public double?  PressureMidfoot { get; set; }
        public double?  PressureForefoot{ get; set; }
        public int      Steps           { get; set; }
        public int      ReadingCount    { get; set; }
    }

    // A single tile on the Biomechanical panel.
    //
    // Value is null when the metric cannot be derived from the hardware actually attached —
    // Unavailable then says why. That is deliberate: a metric that needs both feet must read
    // as unavailable rather than borrow the one foot's number and present it as symmetry.
    public class InsoleMetricDto
    {
        public string  Label       { get; set; } = "";
        public double? Value       { get; set; }   // 0-100, drives the bar
        public string? Sub         { get; set; }   // the measured value in real units
        public string  Trend       { get; set; } = "stable";
        public string? Unavailable { get; set; }
    }

    public class InsoleTrendDto
    {
        public int Days { get; set; }
        public List<InsoleTrendPointDto> Points { get; set; } = new();
        public List<InsoleMetricDto> Biomechanics { get; set; } = new();
        public int TotalReadings { get; set; }
        public int TotalSteps { get; set; }
        public DateTime? FirstReadingAt { get; set; }
        public DateTime? LastReadingAt { get; set; }
    }

    public class InsoleSummaryDto
    {
        public double? AvgCadence            { get; set; }
        public double? AvgBalanceScore        { get; set; }
        public double? AvgStrideAsymmetryPct  { get; set; }
        public double? AvgImpactForce         { get; set; }
        public int     ReadingCount           { get; set; }
        public DateTime? LastReadingAt        { get; set; }

        // Top Speed has no insole/wearable source yet — Forge Insole hasn't shipped.
        // Video analysis is the only source of TopSpeedKmh and (as a fallback, when no
        // insole readings exist) AvgBalanceScore right now. These *Source fields tell the
        // frontend where a value came from so it never has to guess or show a fabricated
        // number — "video" | "insole" | null (field genuinely unavailable).
        public double? TopSpeedKmh           { get; set; }
        public string? TopSpeedSource        { get; set; }
        public string? BalanceScoreSource    { get; set; }
    }
}
