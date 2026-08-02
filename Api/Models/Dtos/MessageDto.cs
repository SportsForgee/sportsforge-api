namespace Api.Models.Dtos
{
    public class MessageDto
    {
        public int     Id         { get; set; }
        public string  SenderId   { get; set; } = "";
        public string  SenderName { get; set; } = "";
        public string? ReceiverId { get; set; }
        public string? ChannelId  { get; set; }
        public string  Content    { get; set; } = "";
        public string  Type       { get; set; } = "text";
        public string? Metadata   { get; set; }
        public DateTime SentAt   { get; set; }
        public bool    IsRead     { get; set; }
    }
}
