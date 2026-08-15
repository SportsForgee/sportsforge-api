namespace ForgeInsole.Data.Entities
{
    public class TelemetryReading
    {
        public long Id { get; set; }
        public string InsoleId { get; set; } = "";
        public DateTime Timestamp { get; set; }

        // Pressure zones (0-100), same heel/midfoot/forefoot layout as the Session-6 forge-insole-sim.
        public double PressureHeel { get; set; }
        public double PressureMidfoot { get; set; }
        public double PressureForefoot { get; set; }

        // IMU — new for this service; the original JS simulator has no accel/gyro output.
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }

        // Derived metrics, ported from forge-insole-sim's nextReading().
        public double Cadence { get; set; }
        public double GaitBalance { get; set; }
        public double ContactTimeMs { get; set; }
        public string FootStrike { get; set; } = "";
        public double StrideAsymmetryPct { get; set; }
        public double ImpactForce { get; set; }

        public Guid? SimulationSessionId { get; set; }
    }
}
