namespace Api.Models.Dtos
{
    public class ConversationDto
    {
        public string   UserId            { get; set; } = "";
        public string   Name              { get; set; } = "";
        public string   Role              { get; set; } = "";
        public string?  Organisation      { get; set; }
        public string?  LastMessage       { get; set; }
        public DateTime? LastMessageAt    { get; set; }
        public bool     LastMessageIsMine { get; set; }
        public int      UnreadCount       { get; set; }
    }
}
