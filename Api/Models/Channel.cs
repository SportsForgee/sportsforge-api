namespace Api.Models
{
    public class Channel
    {
        public string Id          { get; set; } = Guid.NewGuid().ToString();
        public string Name        { get; set; } = "";
        public string Type        { get; set; } = "general"; // team | medical | general
        public string CreatedById { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ChannelMember> Members  { get; set; } = new List<ChannelMember>();
        public ICollection<Message>       Messages { get; set; } = new List<Message>();
    }
}
