namespace Api.Models.Dtos
{
    public class CreateChannelRequest
    {
        public string       Name      { get; set; } = "";
        public string       Type      { get; set; } = "general"; // team | medical | general
        public List<string> MemberIds { get; set; } = new();
    }
}
