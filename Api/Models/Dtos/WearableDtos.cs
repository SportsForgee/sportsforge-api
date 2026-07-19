namespace Api.Models.Dtos
{
    public class WearableReadingInput
    {
        public DateTime Timestamp     { get; set; }
        public int?     HeartRate     { get; set; }
        public double?  RecoveryScore { get; set; }
        public double?  StressLevel   { get; set; }
        public double?  SpO2          { get; set; }
        public double?  HydrationPct  { get; set; }
        public double?  StaminaPct    { get; set; }
    }

    public class WearableIngestRequest
    {
        public string  DeviceSerial   { get; set; } = "";
        public string  AthleteId      { get; set; } = "";
        public int?    BatteryPercent { get; set; }
        public List<WearableReadingInput> Readings { get; set; } = new();
    }

    public class WearableReadingDto
    {
        public DateTime Timestamp     { get; set; }
        public int?     HeartRate     { get; set; }
        public double?  RecoveryScore { get; set; }
        public double?  StressLevel   { get; set; }
        public double?  SpO2          { get; set; }
        public double?  HydrationPct  { get; set; }
        public double?  StaminaPct    { get; set; }
    }

    public class WearableSummaryDto
    {
        public double?   AvgHeartRate     { get; set; }
        public double?   AvgRecoveryScore { get; set; }
        public double?   AvgStressLevel   { get; set; }
        public double?   AvgHydrationPct  { get; set; }
        public double?   AvgStaminaPct    { get; set; }
        public int       ReadingCount     { get; set; }
        public DateTime? LastReadingAt    { get; set; }
    }
}
