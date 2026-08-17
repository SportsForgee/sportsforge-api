using System.Text.Json;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public class HardwareTelemetryService : IHardwareTelemetryService
    {
        // A device is considered "Connected" if it has synced within this window.
        // A live insole syncs every poll (~1s), so silence for this long means it is gone.
        // This was 90s, which made an unplugged device keep reporting "Connected" for a
        // minute and a half — long enough that the UI looked simply wrong.
        private static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(12);

        private readonly AppDbContext _db;
        private readonly IHubContext<VitalsHub> _hub;

        public HardwareTelemetryService(AppDbContext db, IHubContext<VitalsHub> hub)
        {
            _db  = db;
            _hub = hub;
        }

        public async Task<Device> GetOrRegisterDeviceAsync(string athleteId, string type, string serialNumber, string? firmwareVersion = null)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);
            if (device == null)
            {
                device = new Device
                {
                    AthleteId       = athleteId,
                    Type            = type,
                    SerialNumber    = serialNumber,
                    FirmwareVersion = firmwareVersion,
                };
                _db.Devices.Add(device);
                await _db.SaveChangesAsync();
            }
            return device;
        }

        public async Task IngestInsoleReadingsAsync(InsoleIngestRequest request)
        {
            var device = await GetOrRegisterDeviceAsync(request.AthleteId, "Insole", request.DeviceSerial);

            var entities = request.Readings.Select(r => new InsoleReading
            {
                DeviceId           = device.Id,
                AthleteId          = request.AthleteId,
                Timestamp          = r.Timestamp,
                Foot               = r.Foot,
                PressureMapJson    = r.PressureMap == null ? null : JsonSerializer.Serialize(r.PressureMap),
                Cadence            = r.Cadence,
                GroundContactMs    = r.GroundContactMs,
                FootStrike         = r.FootStrike,
                StrideAsymmetryPct = r.StrideAsymmetryPct,
                ImpactForce        = r.ImpactForce,
                BalanceScore       = r.BalanceScore,
                Steps              = r.Steps,
                AccelX             = r.AccelX,
                AccelY             = r.AccelY,
                AccelZ             = r.AccelZ,
                GyroX              = r.GyroX,
                GyroY              = r.GyroY,
                GyroZ              = r.GyroZ,
            }).ToList();

            _db.InsoleReadings.AddRange(entities);

            await RecordSyncAsync(device, request.Source, entities.Count);
            await _db.SaveChangesAsync();

            var latest = entities.OrderByDescending(e => e.Timestamp).FirstOrDefault();
            if (latest != null)
            {
                await _hub.Clients.Group($"athlete-{request.AthleteId}")
                    .SendAsync("InsoleUpdate", ToDto(latest));
            }
        }

        public async Task IngestWearableReadingsAsync(WearableIngestRequest request)
        {
            var device = await GetOrRegisterDeviceAsync(request.AthleteId, "Wearable", request.DeviceSerial);
            if (request.BatteryPercent.HasValue) device.BatteryPercent = request.BatteryPercent;

            var entities = request.Readings.Select(r => new WearableReading
            {
                DeviceId      = device.Id,
                AthleteId     = request.AthleteId,
                Timestamp     = r.Timestamp,
                HeartRate     = r.HeartRate,
                RecoveryScore = r.RecoveryScore,
                StressLevel   = r.StressLevel,
                SpO2          = r.SpO2,
                HydrationPct  = r.HydrationPct,
                StaminaPct    = r.StaminaPct,
            }).ToList();

            _db.WearableReadings.AddRange(entities);

            await RecordSyncAsync(device, "Live", entities.Count);
            await _db.SaveChangesAsync();

            var latest = entities.OrderByDescending(e => e.Timestamp).FirstOrDefault();
            if (latest != null)
            {
                await _hub.Clients.Group($"athlete-{request.AthleteId}")
                    .SendAsync("WearableUpdate", ToDto(latest));
            }
        }

        private async Task RecordSyncAsync(Device device, string source, int packetCount)
        {
            device.LastSyncAt = DateTime.UtcNow;
            device.Status     = "Connected";

            _db.SyncSessions.Add(new SyncSession
            {
                DeviceId    = device.Id,
                StartedAt   = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                PacketCount = packetCount,
                Source      = source,
            });

            await Task.CompletedTask;
        }

        public async Task<List<DeviceDto>> GetDevicesForAthleteAsync(string athleteId)
        {
            var devices = await _db.Devices
                .Where(d => d.AthleteId == athleteId)
                .ToListAsync();

            return devices.Select(ToDto).ToList();
        }

        public async Task<InsoleReadingDto?> GetLatestInsoleReadingAsync(string athleteId)
        {
            var reading = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            return reading == null ? null : ToDto(reading);
        }

        public async Task<WearableReadingDto?> GetLatestWearableReadingAsync(string athleteId)
        {
            var reading = await _db.WearableReadings
                .Where(r => r.AthleteId == athleteId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            return reading == null ? null : ToDto(reading);
        }

        public async Task<InsoleTrendDto> GetInsoleTrendAsync(string athleteId, int days)
        {
            days = Math.Clamp(days, 1, 365);
            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

            // Aggregated in SQL rather than pulled into memory: at ~4 readings/second a
            // 90-day window is millions of rows, and only the daily averages are wanted.
            var daily = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId && r.Timestamp >= since)
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    GaitBalance = g.Average(r => r.BalanceScore),
                    Cadence = g.Average(r => r.Cadence),
                    ContactTimeMs = g.Average(r => r.GroundContactMs),
                    AsymmetryPct = g.Average(r => r.StrideAsymmetryPct),
                    ImpactForce = g.Average(r => r.ImpactForce),
                    // Steps are cumulative per device session, so the day's total is the
                    // highest value seen that day — summing per-reading counts would multiply
                    // every step by the number of readings that reported it.
                    Steps = g.Max(r => r.Steps) ?? 0,
                    ReadingCount = g.Count(),
                })
                .ToListAsync();

            var byDate = daily.ToDictionary(d => d.Date);

            // Pressure lives in a JSON column, so it can't be averaged in SQL. Only days that
            // actually have readings are fetched, and only the columns needed.
            var pressureByDate = new Dictionary<DateTime, (double heel, double mid, double fore)>();
            if (byDate.Count > 0)
            {
                var maps = await _db.InsoleReadings
                    .Where(r => r.AthleteId == athleteId && r.Timestamp >= since && r.PressureMapJson != null)
                    .Select(r => new { r.Timestamp, r.PressureMapJson })
                    .ToListAsync();

                foreach (var group in maps.GroupBy(m => m.Timestamp.Date))
                {
                    var parsed = group.Select(m => ParsePressure(m.PressureMapJson))
                                      .Where(p => p is not null)
                                      .ToList();
                    if (parsed.Count == 0) continue;
                    pressureByDate[group.Key] = (
                        parsed.Average(p => p!.Heel),
                        parsed.Average(p => p!.Midfoot),
                        parsed.Average(p => p!.Forefoot));
                }
            }

            // Every day in the window appears, including empty ones, so a gap in training
            // reads as a gap on the chart instead of two distant days sitting side by side.
            var points = new List<InsoleTrendPointDto>();
            for (var i = 0; i < days; i++)
            {
                var date = since.AddDays(i);
                byDate.TryGetValue(date, out var d);
                pressureByDate.TryGetValue(date, out var p);

                points.Add(new InsoleTrendPointDto
                {
                    Date = date,
                    Label = days <= 7 ? date.ToString("ddd")[..1]
                          : days <= 31 ? date.Day.ToString()
                          : date.ToString("d MMM"),
                    GaitBalance = d?.GaitBalance is double gb ? Math.Round(gb, 1) : null,
                    Cadence = d?.Cadence is double c ? Math.Round(c, 1) : null,
                    ContactTimeMs = d?.ContactTimeMs is double ct ? Math.Round(ct) : null,
                    AsymmetryPct = d?.AsymmetryPct is double a ? Math.Round(a, 1) : null,
                    ImpactForce = d?.ImpactForce is double imp ? Math.Round(imp, 2) : null,
                    PressureHeel = pressureByDate.ContainsKey(date) ? Math.Round(p.heel, 1) : null,
                    PressureMidfoot = pressureByDate.ContainsKey(date) ? Math.Round(p.mid, 1) : null,
                    PressureForefoot = pressureByDate.ContainsKey(date) ? Math.Round(p.fore, 1) : null,
                    Steps = d?.Steps ?? 0,
                    ReadingCount = d?.ReadingCount ?? 0,
                });
            }

            var bounds = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId)
                .GroupBy(_ => 1)
                .Select(g => new { First = g.Min(r => r.Timestamp), Last = g.Max(r => r.Timestamp) })
                .FirstOrDefaultAsync();

            return new InsoleTrendDto
            {
                Days = days,
                Points = points,
                Biomechanics = BuildBiomechanics(points),
                TotalReadings = points.Sum(p => p.ReadingCount),
                TotalSteps = points.Sum(p => p.Steps),
                FirstReadingAt = bounds?.First,
                LastReadingAt = bounds?.Last,
            };
        }

        /// Direction of travel across the window: mean of the first half vs the second.
        private static string Trend(IEnumerable<double> series, bool higherIsBetter = true)
        {
            var v = series.ToList();
            if (v.Count < 4) return "stable";
            var half = v.Count / 2;
            var before = v.Take(half).Average();
            var after = v.Skip(half).Average();
            if (before == 0) return "stable";
            var changePct = (after - before) / Math.Abs(before) * 100;
            if (Math.Abs(changePct) < 2) return "stable";
            var improving = changePct > 0 == higherIsBetter;
            return improving ? "up" : "down";
        }

        private static List<InsoleMetricDto> BuildBiomechanics(List<InsoleTrendPointDto> points)
        {
            var withData = points.Where(p => p.ReadingCount > 0).ToList();

            InsoleMetricDto Unavailable(string label, string why) =>
                new() { Label = label, Value = null, Unavailable = why };

            if (withData.Count == 0)
            {
                return new List<InsoleMetricDto>
                {
                    Unavailable("Gait Balance", "No readings yet"),
                    Unavailable("Load Symmetry", "No readings yet"),
                    Unavailable("Ground Contact", "No readings yet"),
                    Unavailable("Impact Load", "No readings yet"),
                    Unavailable("Stride Symmetry", "Needs a second insole"),
                };
            }

            // Weighted by how many readings each day contributed.
            //
            // Averaging the daily averages would let a two-minute session count as much as a
            // three-hour one, so the headline number could sit far from the athlete's actual
            // average — measured at 530ms day-weighted against 428ms reading-weighted on the
            // same data. The tiles say "your average", so they weight by reading.
            double WeightedMean(Func<InsoleTrendPointDto, double?> pick)
            {
                var rows = withData.Where(p => pick(p).HasValue && p.ReadingCount > 0).ToList();
                if (rows.Count == 0) return 0;
                var totalWeight = rows.Sum(p => (long)p.ReadingCount);
                return rows.Sum(p => pick(p)!.Value * p.ReadingCount) / totalWeight;
            }

            // Kept as series (one entry per day) purely for the trend direction, which is a
            // day-over-day comparison and should not be reading-weighted.
            var balance = withData.Where(p => p.GaitBalance.HasValue).Select(p => p.GaitBalance!.Value).ToList();
            var asym = withData.Where(p => p.AsymmetryPct.HasValue).Select(p => p.AsymmetryPct!.Value).ToList();
            var contact = withData.Where(p => p.ContactTimeMs.HasValue).Select(p => p.ContactTimeMs!.Value).ToList();
            var impact = withData.Where(p => p.ImpactForce.HasValue).Select(p => p.ImpactForce!.Value).ToList();

            var balanceMean = WeightedMean(p => p.GaitBalance);
            var asymMean = WeightedMean(p => p.AsymmetryPct);
            var contactMean = WeightedMean(p => p.ContactTimeMs);
            var impactMean = WeightedMean(p => p.ImpactForce);

            var metrics = new List<InsoleMetricDto>();

            metrics.Add(balance.Count == 0
                ? Unavailable("Gait Balance", "No readings yet")
                : new InsoleMetricDto
                {
                    Label = "Gait Balance",
                    Value = Math.Round(balanceMean),
                    Sub = $"{balanceMean:0.#}% centred",
                    Trend = Trend(balance),
                });

            metrics.Add(asym.Count == 0
                ? Unavailable("Load Symmetry", "No readings yet")
                : new InsoleMetricDto
                {
                    Label = "Load Symmetry",
                    Value = Math.Round(Math.Clamp(100 - asymMean, 0, 100)),
                    Sub = $"{asymMean:0.#}% inside-outside spread",
                    // Lower spread is better, so the direction is inverted.
                    Trend = Trend(asym, higherIsBetter: false),
                });

            metrics.Add(contact.Count == 0
                ? Unavailable("Ground Contact", "No readings yet")
                : new InsoleMetricDto
                {
                    Label = "Ground Contact",
                    // Inverted (shorter contact scores higher) against a 1000ms ceiling. The
                    // ceiling has to cover walking, where contact runs 600-800ms — a
                    // running-only 400ms scale pins every walking session at 0 and makes a
                    // real measurement look like missing data. The true value is in Sub.
                    Value = Math.Round(Math.Clamp(100 - contactMean / 1000 * 100, 0, 100)),
                    Sub = $"{contactMean:0} ms",
                    Trend = Trend(contact, higherIsBetter: false),
                });

            metrics.Add(impact.Count == 0
                ? Unavailable("Impact Load", "No readings yet")
                : new InsoleMetricDto
                {
                    Label = "Impact Load",
                    Value = Math.Round(Math.Clamp(impactMean / 3.0 * 100, 0, 100)),
                    Sub = $"{impactMean:0.00} index",
                    Trend = Trend(impact, higherIsBetter: false),
                });

            // Comparing left against right needs two insoles; one unit can only ever report
            // its own foot, so this stays explicitly unavailable rather than guessing.
            metrics.Add(Unavailable("Stride Symmetry", "Needs a second insole"));

            return metrics;
        }

        public async Task<InsoleSummaryDto> GetInsoleSummaryAsync(string athleteId, TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var readings = await _db.InsoleReadings
                .Where(r => r.AthleteId == athleteId && r.Timestamp >= since)
                .ToListAsync();

            // Forge Insole hasn't shipped — Top Speed has no insole/wearable source at all,
            // and Balance falls back to the latest analyzed video when no insole readings
            // exist yet. Once real insole data exists it always wins for balance.
            var latestVideoResult = await _db.VideoUploads
                .Where(v => v.AthleteId == athleteId)
                .Join(_db.VideoAnalysisResults, v => v.Id, r => r.VideoUploadId, (v, r) => r)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            var topSpeed = latestVideoResult?.TopSpeedKmh;
            var topSpeedSource = topSpeed.HasValue ? "video" : null;

            if (readings.Count == 0)
            {
                return new InsoleSummaryDto
                {
                    ReadingCount        = 0,
                    AvgBalanceScore     = latestVideoResult?.GaitBalanceScore,
                    BalanceScoreSource  = latestVideoResult?.GaitBalanceScore.HasValue == true ? "video" : null,
                    TopSpeedKmh         = topSpeed,
                    TopSpeedSource      = topSpeedSource,
                };
            }

            return new InsoleSummaryDto
            {
                AvgCadence           = readings.Where(r => r.Cadence.HasValue).Select(r => r.Cadence!.Value).DefaultIfEmpty().Average(),
                AvgBalanceScore      = readings.Where(r => r.BalanceScore.HasValue).Select(r => r.BalanceScore!.Value).DefaultIfEmpty().Average(),
                BalanceScoreSource   = "insole",
                AvgStrideAsymmetryPct= readings.Where(r => r.StrideAsymmetryPct.HasValue).Select(r => r.StrideAsymmetryPct!.Value).DefaultIfEmpty().Average(),
                AvgImpactForce       = readings.Where(r => r.ImpactForce.HasValue).Select(r => r.ImpactForce!.Value).DefaultIfEmpty().Average(),
                ReadingCount         = readings.Count,
                LastReadingAt        = readings.Max(r => r.Timestamp),
                TopSpeedKmh          = topSpeed,
                TopSpeedSource       = topSpeedSource,
            };
        }

        public async Task<WearableSummaryDto> GetWearableSummaryAsync(string athleteId, TimeSpan window)
        {
            var since = DateTime.UtcNow - window;
            var readings = await _db.WearableReadings
                .Where(r => r.AthleteId == athleteId && r.Timestamp >= since)
                .ToListAsync();

            if (readings.Count == 0)
                return new WearableSummaryDto { ReadingCount = 0 };

            return new WearableSummaryDto
            {
                AvgHeartRate     = readings.Where(r => r.HeartRate.HasValue).Select(r => (double)r.HeartRate!.Value).DefaultIfEmpty().Average(),
                AvgRecoveryScore = readings.Where(r => r.RecoveryScore.HasValue).Select(r => r.RecoveryScore!.Value).DefaultIfEmpty().Average(),
                AvgStressLevel   = readings.Where(r => r.StressLevel.HasValue).Select(r => r.StressLevel!.Value).DefaultIfEmpty().Average(),
                AvgHydrationPct  = readings.Where(r => r.HydrationPct.HasValue).Select(r => r.HydrationPct!.Value).DefaultIfEmpty().Average(),
                AvgStaminaPct    = readings.Where(r => r.StaminaPct.HasValue).Select(r => r.StaminaPct!.Value).DefaultIfEmpty().Average(),
                ReadingCount     = readings.Count,
                LastReadingAt    = readings.Max(r => r.Timestamp),
            };
        }

        private static DeviceDto ToDto(Device d) => new()
        {
            Id              = d.Id,
            Type            = d.Type,
            SerialNumber    = d.SerialNumber,
            FirmwareVersion = d.FirmwareVersion,
            PairedAt        = d.PairedAt,
            LastSyncAt      = d.LastSyncAt,
            BatteryPercent  = d.BatteryPercent,
            Status          = d.LastSyncAt.HasValue && DateTime.UtcNow - d.LastSyncAt.Value <= ConnectedWindow
                                ? "Connected"
                                : "Disconnected",
        };

        private static readonly JsonSerializerOptions PressureJsonOptions = new(JsonSerializerDefaults.Web);

        // PressureMapJson is free-form by design, so a reading written by some other producer
        // may not match InsolePressureDto at all. A parse failure must not take down the whole
        // reading — the caller still wants cadence, strike and contact time — so it degrades
        // to a null pressure map instead of throwing.
        private static InsolePressureDto? ParsePressure(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<InsolePressureDto>(json, PressureJsonOptions); }
            catch (JsonException) { return null; }
        }

        private static InsoleReadingDto ToDto(InsoleReading r) => new()
        {
            Timestamp          = r.Timestamp,
            Foot               = r.Foot,
            Cadence            = r.Cadence,
            GroundContactMs    = r.GroundContactMs,
            FootStrike         = r.FootStrike,
            StrideAsymmetryPct = r.StrideAsymmetryPct,
            ImpactForce        = r.ImpactForce,
            BalanceScore       = r.BalanceScore,
            Steps              = r.Steps,
            Pressure           = ParsePressure(r.PressureMapJson),
            // All six axes null means no IMU sample was recorded at all; a real MPU6050 frame
            // always carries gravity on at least one axis.
            Imu = r.AccelX is null && r.AccelY is null && r.AccelZ is null
               && r.GyroX is null && r.GyroY is null && r.GyroZ is null
                ? null
                : new InsoleImuDto
                {
                    Accel = new Vector3Dto { X = r.AccelX ?? 0, Y = r.AccelY ?? 0, Z = r.AccelZ ?? 0 },
                    Gyro  = new Vector3Dto { X = r.GyroX  ?? 0, Y = r.GyroY  ?? 0, Z = r.GyroZ  ?? 0 },
                },
        };

        private static WearableReadingDto ToDto(WearableReading r) => new()
        {
            Timestamp     = r.Timestamp,
            HeartRate     = r.HeartRate,
            RecoveryScore = r.RecoveryScore,
            StressLevel   = r.StressLevel,
            SpO2          = r.SpO2,
            HydrationPct  = r.HydrationPct,
            StaminaPct    = r.StaminaPct,
        };
    }
}
