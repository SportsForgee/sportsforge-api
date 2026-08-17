namespace Api.Models
{
    public class InsoleReading
    {
        public int      Id                 { get; set; }
        public int      DeviceId           { get; set; }
        public string   AthleteId          { get; set; } = "";
        public DateTime Timestamp          { get; set; }
        public string   Foot               { get; set; } = "";   // L | R
        public string?  PressureMapJson    { get; set; }         // free-form JSON, sensor layout still being redesigned
        public double?  Cadence            { get; set; }         // steps/min
        public double?  GroundContactMs    { get; set; }
        public string?  FootStrike         { get; set; }         // Heel | Midfoot | Forefoot
        public double?  StrideAsymmetryPct { get; set; }
        public double?  ImpactForce        { get; set; }
        public double?  BalanceScore       { get; set; }         // 0-100
        public int?     Steps              { get; set; }         // cumulative for the device's session

        // MPU6050 motion. Nullable because readings that predate this, and any producer
        // without an IMU, legitimately have none.
        public double?  AccelX             { get; set; }         // m/s^2
        public double?  AccelY             { get; set; }
        public double?  AccelZ             { get; set; }
        public double?  GyroX              { get; set; }         // deg/s
        public double?  GyroY              { get; set; }
        public double?  GyroZ              { get; set; }

        public Device?  Device             { get; set; }
        public AppUser? Athlete            { get; set; }
    }
}
