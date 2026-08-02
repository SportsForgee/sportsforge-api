namespace Api.Models
{
    public class Device
    {
        public int      Id              { get; set; }
        public string   AthleteId       { get; set; } = "";
        public string   Type            { get; set; } = "";       // Insole | Wearable
        public string   SerialNumber    { get; set; } = "";
        public string?  FirmwareVersion { get; set; }
        public DateTime PairedAt        { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncAt     { get; set; }
        public int?     BatteryPercent  { get; set; }
        public string   Status          { get; set; } = "Disconnected"; // Connected | Disconnected | Syncing

        public AppUser? Athlete { get; set; }
    }
}
