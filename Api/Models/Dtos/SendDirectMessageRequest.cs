namespace Api.Models.Dtos
{
    public class SendDirectMessageRequest
    {
        public string  ReceiverId { get; set; } = "";
        public string  Content    { get; set; } = "";
        public string  Type       { get; set; } = "text";
        public string? Metadata   { get; set; }
    }
}
