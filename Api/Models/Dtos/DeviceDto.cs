namespace Api.Models.Dtos
{
    public class DeviceDto
    {
        public int       Id              { get; set; }
        public string    Type            { get; set; } = "";
        public string    SerialNumber    { get; set; } = "";
        public string?   FirmwareVersion { get; set; }
        public DateTime  PairedAt        { get; set; }
        public DateTime? LastSyncAt      { get; set; }
        public int?      BatteryPercent  { get; set; }
        public string    Status          { get; set; } = "";
    }

    public class PairDeviceRequest
    {
        public string  Type            { get; set; } = ""; // Insole | Wearable
        public string  SerialNumber    { get; set; } = "";
        public string? FirmwareVersion { get; set; }
    }
}
