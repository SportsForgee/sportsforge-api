namespace Api.Models
{
    public class Drill
    {
        public int    Id          { get; set; }
        public string CoachId     { get; set; } = "";
        public string Title       { get; set; } = "";
        public string Description { get; set; } = "";
        public int    Sets        { get; set; }
        public int    Reps        { get; set; }
        public string Duration    { get; set; } = "";
        public string Intensity   { get; set; } = "medium"; // low | medium | high
        public string Category    { get; set; } = "general"; // strength | endurance | skill | speed | recovery | general
        public string? VideoUrl   { get; set; }
        public bool   IsActive    { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public AppUser? Coach { get; set; }
    }
}
