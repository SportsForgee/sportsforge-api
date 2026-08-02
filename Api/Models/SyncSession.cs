namespace Api.Models
{
    public class SyncSession
    {
        public int       Id           { get; set; }
        public int       DeviceId     { get; set; }
        public DateTime  StartedAt    { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt  { get; set; }
        public int       PacketCount  { get; set; }
        public string    Source       { get; set; } = "Live"; // Live | OfflineSync

        public Device?   Device       { get; set; }
    }
}
