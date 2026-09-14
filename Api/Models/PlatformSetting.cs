namespace Api.Models
{
    public class PlatformSetting
    {
        public string   Key             { get; set; } = "";
        public string   Value           { get; set; } = "";
        public string?  Description     { get; set; }
        public DateTime UpdatedAt       { get; set; } = DateTime.UtcNow;
        public string?  UpdatedByUserId { get; set; }
    }
}
