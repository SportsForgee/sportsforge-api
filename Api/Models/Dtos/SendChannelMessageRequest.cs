namespace Api.Models.Dtos
{
    public class SendChannelMessageRequest
    {
        public string  Content  { get; set; } = "";
        public string  Type     { get; set; } = "text";
        public string? Metadata { get; set; }
    }
}
