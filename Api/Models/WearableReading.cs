namespace Api.Models
{
    public class WearableReading
    {
        public int      Id             { get; set; }
        public int      DeviceId       { get; set; }
        public string   AthleteId      { get; set; } = "";
        public DateTime Timestamp      { get; set; }
        public int?     HeartRate      { get; set; }  // bpm
        public double?  RecoveryScore  { get; set; }  // 0-100
        public double?  StressLevel    { get; set; }  // 0-100
        public double?  SpO2           { get; set; }  // 0-100 %
        public double?  HydrationPct   { get; set; }  // 0-100
        public double?  StaminaPct     { get; set; }  // 0-100

        public Device?  Device         { get; set; }
        public AppUser? Athlete        { get; set; }
    }
}
