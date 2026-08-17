using ForgeInsole.Data.Entities;

namespace ForgeInsole.Api.Services
{
    // Per-insole running state (cadence/balance/asymmetry random-walk), mirroring the
    // module-level mutable state in hardware-integration/simulators/forge-insole-sim/index.mjs —
    // except keyed per insole here instead of a single global device.
    public class InsoleGeneratorState
    {
        public double Cadence = 168;
        public double GaitBalance = 90;
        public double Asymmetry = 5;
    }

    // Pressure-zone and derived-metric generation is a direct port of forge-insole-sim's
    // randomWalk()/jitter()/nextReading(). IMU (accel/gyro) is new — the JS simulator never
    // emitted IMU data, so that part isn't "ported," it's new generation logic.
    public static class TelemetryGenerator
    {
        private static readonly Random Rng = new();

        public static double RandomWalk(double current, double min, double max, double step)
        {
            var next = current + (Rng.NextDouble() - 0.5) * step;
            return Math.Min(max, Math.Max(min, next));
        }

        public static double Jitter(double baseValue, double spread)
            => baseValue + (Rng.NextDouble() - 0.5) * spread;

        public static TelemetryReading NextReading(string insoleId, InsoleSide side, InsoleGeneratorState state)
        {
            state.Cadence = RandomWalk(state.Cadence, 150, 190, 4);
            state.GaitBalance = RandomWalk(state.GaitBalance, 60, 98, 3);
            state.Asymmetry = RandomWalk(state.Asymmetry, 1, 22, 2.5);

            var footStrike = Rng.NextDouble() > 0.15
                ? "Midfoot"
                : (Rng.NextDouble() > 0.5 ? "Heel" : "Forefoot");

            return new TelemetryReading
            {
                InsoleId = insoleId,
                Timestamp = DateTime.UtcNow,

                PressureHeel = Math.Round(Jitter(side == InsoleSide.L ? 58 : 55, 15), 1),
                PressureMidfoot = Math.Round(Jitter(38, 10), 1),
                PressureForefoot = Math.Round(Jitter(side == InsoleSide.L ? 74 : 70, 15), 1),

                AccelX = Math.Round(Jitter(0, 1.2), 3),
                AccelY = Math.Round(Jitter(0, 1.2), 3),
                AccelZ = Math.Round(Jitter(9.81, 0.6), 3),
                GyroX = Math.Round(Jitter(0, 15), 2),
                GyroY = Math.Round(Jitter(0, 15), 2),
                GyroZ = Math.Round(Jitter(0, 15), 2),

                Cadence = Math.Round(state.Cadence),
                GaitBalance = Math.Round(state.GaitBalance),
                ContactTimeMs = Math.Round(Jitter(230, 25)),
                FootStrike = footStrike,
                StrideAsymmetryPct = Math.Round(state.Asymmetry * 10) / 10,
                ImpactForce = Math.Round(Jitter(1.9, 0.6) * 10) / 10,
            };
        }
    }
}
