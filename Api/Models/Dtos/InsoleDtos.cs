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
    }
}
