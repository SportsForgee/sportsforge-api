namespace Api.Models.Dtos
{
    public class DrillDto
    {
        public int    Id          { get; set; }
        public string Title       { get; set; } = "";
        public string Description { get; set; } = "";
        public int    Sets        { get; set; }
        public int    Reps        { get; set; }
        public string Duration    { get; set; } = "";
        public string Intensity   { get; set; } = "";
        public string Category    { get; set; } = "";
        public string? VideoUrl   { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
