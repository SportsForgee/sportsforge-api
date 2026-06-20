namespace Api.Models
{
    public class ChannelMember
    {
        public int    Id        { get; set; }
        public string ChannelId { get; set; } = "";
        public string UserId    { get; set; } = "";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public Channel? Channel { get; set; }
        public AppUser? User    { get; set; }
    }
}
