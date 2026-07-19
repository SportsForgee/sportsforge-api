namespace KhoiIntegration
{
    // Shape of one reading as returned by the Khoi Cloud API.
    // See hardware-integration/docs/khoi-api-contract.md — field names/auth are assumed,
    // not yet confirmed with Khoi Tech.
    public class KhoiCloudReading
    {
        public DateTime Timestamp     { get; set; }
        public int?     HeartRate     { get; set; }
        public double?  RecoveryScore { get; set; }
        public double?  StressLevel   { get; set; }
        public double?  SpO2          { get; set; }
        public double?  HydrationPct  { get; set; }
        public double?  StaminaPct    { get; set; }
    }

    public class KhoiCloudResponse
    {
        public string  AthleteId      { get; set; } = "";
        public string  DeviceSerial   { get; set; } = "";
        public int?    BatteryPercent { get; set; }
        public List<KhoiCloudReading> Readings { get; set; } = new();
    }
}
