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

        // Cumulative step count for the device's current session. The generator has no notion
        // of steps (it invents cadence directly), so this stays 0 for simulated readings — it's
        // only meaningful on rows with Source == Device.
        public int Steps { get; set; }

        // Same axis as Insole.Source, recorded per reading so a table holding both simulated
        // history and live device data stays self-describing after the fact.
        public InsoleSource Source { get; set; } = InsoleSource.Simulated;

        public Guid? SimulationSessionId { get; set; }
    }
}
