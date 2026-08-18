using ForgeInsole.Data.Entities;

namespace ForgeInsole.Api.Devices
{
    // Translates one firmware telemetry frame into a TelemetryReading, so device data and
    // generated data are indistinguishable in shape to every consumer downstream (the SSE
    // stream, the history endpoint, and backend/Api's ForgeInsoleSyncService).
    //
    // The two models don't line up one-to-one, and the gaps are documented per field below
    // rather than papered over — the firmware measures four zones (heel/toe/medial/lateral)
    // while this API's schema is the three-zone heel/midfoot/forefoot layout inherited from
    // forge-insole-sim.
    public static class DeviceFrameMapper
    {
        private const double GravityMetersPerSecondSquared = 9.80665;

        public static TelemetryReading ToReading(InsoleDeviceFrame frame, string insoleId, DeviceIngestOptions options)
        {
            var pressure = frame.Pressure ?? new DeviceZones();
            var imu = frame.Imu ?? new DeviceImu();

            var fullScale = options.PressureFullScaleCounts <= 0 ? 4095 : options.PressureFullScaleCounts;
            double Pct(double counts) => Math.Round(Math.Clamp(counts / fullScale * 100.0, 0, 100), 1);

            // The firmware's left/right FSRs sit either side of the arch, so their mean stands
            // in for the midfoot zone. That is an approximation of a sensor layout this schema
            // was never designed for — the two are averaged (not summed) so a fully loaded
            // midfoot still reads ~100 rather than ~200.
            var midfootCounts = (pressure.Left + pressure.Right) / 2.0;

            // balanceMlPct runs -100 (all load medial/left) to +100 (all lateral/right); 0 is
            // perfectly centred. GaitBalance in this API is "how centred is the load, 0-100",
            // so the magnitude of the deviation is what gets subtracted.
            var balanceMagnitude = Math.Clamp(Math.Abs(frame.BalanceMlPct), 0, 100);

            var countsPerBw = options.CountsPerBodyWeight <= 0 ? 4000 : options.CountsPerBodyWeight;

            return new TelemetryReading
            {
                InsoleId = insoleId,

                // Server-stamped, not device-stamped: frame.Timestamp is millis() since boot,
                // and the ESP32 has no RTC or NTP sync, so it cannot produce a wall clock.
                // At a 10 Hz stream over LAN the arrival time is within a few ms of the sample.
                Timestamp = DateTime.UtcNow,

                PressureHeel = Pct(pressure.Heel),
                PressureMidfoot = Pct(midfootCounts),
                PressureForefoot = Pct(pressure.Toe),

                // Firmware reports g (ACCEL_LSB_PER_G); this API's existing readings are in
                // m/s^2 (the generator centres AccelZ on 9.81), so convert rather than
                // silently mixing units within one column.
                AccelX = Math.Round(imu.Ax * GravityMetersPerSecondSquared, 3),
                AccelY = Math.Round(imu.Ay * GravityMetersPerSecondSquared, 3),
                AccelZ = Math.Round(imu.Az * GravityMetersPerSecondSquared, 3),

                // Both sides are degrees/second — no conversion needed.
                GyroX = Math.Round(imu.Gx, 2),
                GyroY = Math.Round(imu.Gy, 2),
                GyroZ = Math.Round(imu.Gz, 2),

                // Single-foot steps/min. The generator's 150-190 range is a two-foot cadence,
                // so a real device walking normally will read roughly half that. Consumers
                // comparing the two should account for it.
                Cadence = Math.Round(frame.CadenceSpm, 1),

                GaitBalance = Math.Round(100.0 - balanceMagnitude, 1),
                ContactTimeMs = Math.Round(frame.AvgContactMs),

                // "--" is the firmware's "no step classified yet" placeholder.
                FootStrike = string.IsNullOrWhiteSpace(frame.FootStrike) || frame.FootStrike == "--"
                    ? "Unknown"
                    : frame.FootStrike,

                // True stride asymmetry needs both feet compared against each other, and one
                // insole only ever sees its own. This is the medial/lateral load imbalance
                // within this foot — a different measurement that shares the units.
                StrideAsymmetryPct = Math.Round(balanceMagnitude, 1),

                // Relative load index, not body weights — see CountsPerBodyWeight.
                ImpactForce = Math.Round(pressure.Total / countsPerBw, 2),

                Steps = frame.Steps,
                Source = InsoleSource.Device,
            };
        }
    }
}
