namespace Api.Models
{
    public class Message
    {
        public int     Id         { get; set; }
        public string  SenderId   { get; set; } = "";
        public string  SenderName { get; set; } = "";
        public string? ReceiverId { get; set; }   // null for channel messages
        public string? ChannelId  { get; set; }   // null for direct messages
        public string  Content    { get; set; } = "";
        public string  Type       { get; set; } = "text"; // text | drill | health_alert
        public string? Metadata   { get; set; }           // JSON payload for drill cards / alerts
        public DateTime SentAt   { get; set; } = DateTime.UtcNow;
        public bool    IsRead     { get; set; } = false;

        public AppUser? Sender { get; set; }
    }
}
