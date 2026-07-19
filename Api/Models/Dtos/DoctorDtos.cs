namespace Api.Models.Dtos
{
    // Squad-list row — powers the doctor dashboard's Overview / Injury Management /
    // Fitness Tracking / Medical Reports pages (they all filter/sort this same shape).
    public class DoctorAthleteDto
    {
        public string  Id                 { get; set; } = "";
        public string  Name               { get; set; } = "";
        public string  Position           { get; set; } = "";
        public string? Team               { get; set; }
        public int     InjuryRiskScore    { get; set; }
        public int     Performance        { get; set; }
        public int     Fitness            { get; set; }
        public string  Status             { get; set; } = "Fit"; // Fit | Monitor | Critical
        public bool    HasLiveData        { get; set; }          // true once a real/simulated device has reported
        public int?    HeartRate          { get; set; }
        public double? RecoveryScore      { get; set; }
        public double? StrideAsymmetryPct { get; set; }
        public bool    InsoleConnected    { get; set; }
        public bool    WearableConnected  { get; set; }
        public bool    ClearanceGranted   { get; set; } = true;
        public string? Notes              { get; set; }
    }

    // Full detail — powers the Player Health detail page.
    public class AthleteMedicalDetailDto
    {
        public string  Id               { get; set; } = "";
        public string  Name             { get; set; } = "";
        public string  Position         { get; set; } = "";
        public string? Team             { get; set; }
        public string? Nationality      { get; set; }
        public string? Height           { get; set; }
        public string? Weight           { get; set; }
        public int?    JerseyNumber     { get; set; }

        public int     InjuryRiskScore  { get; set; }
        public string  Status           { get; set; } = "Fit";
        public int     Performance      { get; set; }
        public int     Fitness          { get; set; }
        public bool    ClearanceGranted { get; set; } = true;
        public string? Notes            { get; set; }
        public DateTime? NotesUpdatedAt { get; set; }

        public bool                InsoleConnected   { get; set; }
        public InsoleReadingDto?   LatestInsole       { get; set; }
        public InsoleSummaryDto?   InsoleSummary      { get; set; }

        public bool                WearableConnected  { get; set; }
        public WearableReadingDto? LatestWearable      { get; set; }
        public WearableSummaryDto? WearableSummary     { get; set; }
    }

    public class SetClearanceRequest
    {
        public bool Granted { get; set; }
    }

    public class SetNotesRequest
    {
        public string Notes { get; set; } = "";
    }
}
