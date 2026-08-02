using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    // Builds the doctor-facing squad list and per-athlete detail view by combining
    // a stable baseline (deterministic per-athlete, same convention as
    // CoachDashboardService) with real Forge Insole / Khoi wearable telemetry when
    // it exists. Athletes with no device data yet still show a sensible baseline
    // risk score — nothing breaks before hardware ships, and nothing changes in
    // this scoring logic once it does.
    public class DoctorDashboardService : IDoctorDashboardService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly IHardwareTelemetryService _telemetry;

        public DoctorDashboardService(AppDbContext db, UserManager<AppUser> users, IHardwareTelemetryService telemetry)
        {
            _db        = db;
            _users     = users;
            _telemetry = telemetry;
        }

        public async Task<List<DoctorAthleteDto>> GetSquadAsync(string doctorId)
        {
            var doctor = await _users.FindByIdAsync(doctorId);

            var athletes = await _db.Users.Where(u => u.SfRole == "athlete").ToListAsync();
            if (doctor != null && !string.IsNullOrEmpty(doctor.Organisation))
            {
                var sameOrg = athletes.Where(a => a.Organisation == doctor.Organisation).ToList();
                if (sameOrg.Count > 0) athletes = sameOrg;
            }

            var result = new List<DoctorAthleteDto>();
            foreach (var athlete in athletes)
                result.Add(await BuildSquadRowAsync(athlete));

            return result.OrderByDescending(a => a.InjuryRiskScore).ToList();
        }

        public async Task<AthleteMedicalDetailDto?> GetAthleteDetailAsync(string athleteId)
        {
            var athlete = await _users.FindByIdAsync(athleteId);
            if (athlete == null || athlete.SfRole != "athlete") return null;

            var insoleSummary   = await _telemetry.GetInsoleSummaryAsync(athleteId, TimeSpan.FromMinutes(30));
            var wearableSummary = await _telemetry.GetWearableSummaryAsync(athleteId, TimeSpan.FromMinutes(30));
            var latestInsole    = await _telemetry.GetLatestInsoleReadingAsync(athleteId);
            var latestWearable  = await _telemetry.GetLatestWearableReadingAsync(athleteId);
            var devices         = await _telemetry.GetDevicesForAthleteAsync(athleteId);
            var record          = await _db.AthleteMedicalRecords.FirstOrDefaultAsync(r => r.AthleteId == athleteId);

            var (risk, status, performance, fitness) = ComputeRisk(athlete, insoleSummary, wearableSummary);

            return new AthleteMedicalDetailDto
            {
                Id               = athlete.Id,
                Name             = $"{athlete.FirstName} {athlete.LastName}".Trim(),
                Position         = athlete.Position ?? "—",
                Team             = athlete.Team ?? athlete.Organisation,
                Nationality      = athlete.Nationality,
                Height           = athlete.Height,
                Weight           = athlete.Weight,
                JerseyNumber     = athlete.JerseyNumber,

                InjuryRiskScore  = risk,
                Status           = status,
                Performance      = performance,
                Fitness          = fitness,
                ClearanceGranted = record?.ClearanceGranted ?? true,
                Notes            = record?.Notes,
                NotesUpdatedAt   = record?.UpdatedAt,

                InsoleConnected  = devices.Any(d => d.Type == "Insole" && d.Status == "Connected"),
                LatestInsole     = latestInsole,
                InsoleSummary    = insoleSummary,

                WearableConnected = devices.Any(d => d.Type == "Wearable" && d.Status == "Connected"),
                LatestWearable    = latestWearable,
                WearableSummary   = wearableSummary,
            };
        }

        public async Task<bool> SetClearanceAsync(string doctorId, string athleteId, bool granted)
        {
            var athlete = await _users.FindByIdAsync(athleteId);
            if (athlete == null || athlete.SfRole != "athlete") return false;

            var record = await GetOrCreateRecordAsync(athleteId);
            record.ClearanceGranted  = granted;
            record.UpdatedByDoctorId = doctorId;
            record.UpdatedAt         = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetNotesAsync(string doctorId, string athleteId, string notes)
        {
            var athlete = await _users.FindByIdAsync(athleteId);
            if (athlete == null || athlete.SfRole != "athlete") return false;

            var record = await GetOrCreateRecordAsync(athleteId);
            record.Notes             = notes;
            record.UpdatedByDoctorId = doctorId;
            record.UpdatedAt         = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<AthleteMedicalRecord> GetOrCreateRecordAsync(string athleteId)
        {
            var record = await _db.AthleteMedicalRecords.FirstOrDefaultAsync(r => r.AthleteId == athleteId);
            if (record == null)
            {
                record = new AthleteMedicalRecord { AthleteId = athleteId };
                _db.AthleteMedicalRecords.Add(record);
            }
            return record;
        }

        private async Task<DoctorAthleteDto> BuildSquadRowAsync(AppUser athlete)
        {
            var insoleSummary   = await _telemetry.GetInsoleSummaryAsync(athlete.Id, TimeSpan.FromMinutes(30));
            var wearableSummary = await _telemetry.GetWearableSummaryAsync(athlete.Id, TimeSpan.FromMinutes(30));
            var latestWearable  = await _telemetry.GetLatestWearableReadingAsync(athlete.Id);
            var devices         = await _telemetry.GetDevicesForAthleteAsync(athlete.Id);
            var record          = await _db.AthleteMedicalRecords.FirstOrDefaultAsync(r => r.AthleteId == athlete.Id);

            var (risk, status, performance, fitness) = ComputeRisk(athlete, insoleSummary, wearableSummary);

            return new DoctorAthleteDto
            {
                Id                 = athlete.Id,
                Name               = $"{athlete.FirstName} {athlete.LastName}".Trim(),
                Position           = athlete.Position ?? "—",
                Team               = athlete.Team ?? athlete.Organisation,
                InjuryRiskScore    = risk,
                Performance        = performance,
                Fitness            = fitness,
                Status             = status,
                HasLiveData        = insoleSummary.ReadingCount > 0 || wearableSummary.ReadingCount > 0,
                HeartRate          = latestWearable?.HeartRate,
                RecoveryScore      = wearableSummary.AvgRecoveryScore,
                StrideAsymmetryPct = insoleSummary.AvgStrideAsymmetryPct,
                InsoleConnected    = devices.Any(d => d.Type == "Insole" && d.Status == "Connected"),
                WearableConnected  = devices.Any(d => d.Type == "Wearable" && d.Status == "Connected"),
                ClearanceGranted   = record?.ClearanceGranted ?? true,
                Notes              = record?.Notes,
            };
        }

        private static (int risk, string status, int performance, int fitness) ComputeRisk(
            AppUser athlete, InsoleSummaryDto insole, WearableSummaryDto wearable)
        {
            // Deterministic per-athlete baseline so scores are stable across requests
            // and consistent with how CoachDashboardService seeds its placeholder stats.
            var rnd = new Random(athlete.Id.GetHashCode());
            double risk = rnd.Next(5, 40);
            int performance = rnd.Next(70, 96);
            int fitness = rnd.Next(65, 96);

            if (wearable.ReadingCount > 0)
            {
                if (wearable.AvgRecoveryScore is double rec) risk += Math.Max(0, 70 - rec) * 0.4;
                if (wearable.AvgStressLevel   is double str) risk += Math.Max(0, str - 40) * 0.3;
                if (wearable.AvgRecoveryScore is double rec2) fitness = (int)Math.Clamp((rec2 + fitness) / 2, 0, 100);
            }

            if (insole.ReadingCount > 0)
            {
                if (insole.AvgStrideAsymmetryPct is double asym) risk += Math.Max(0, asym - 8) * 1.2;
                if (insole.AvgImpactForce         is double imp) risk += Math.Max(0, imp - 1.8) * 8;
            }

            risk = Math.Clamp(risk, 0, 100);
            var status = risk >= 60 ? "Critical" : risk >= 30 ? "Monitor" : "Fit";

            return ((int)Math.Round(risk), status, performance, fitness);
        }
    }
}
