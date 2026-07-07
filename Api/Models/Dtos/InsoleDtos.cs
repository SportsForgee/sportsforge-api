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
    }

    public class InsoleIngestRequest
    {
        public string   DeviceSerial { get; set; } = "";
        public string   AthleteId    { get; set; } = "";
        public string   Source       { get; set; } = "Live"; // Live | OfflineSync
        public List<InsoleReadingInput> Readings { get; set; } = new();
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
