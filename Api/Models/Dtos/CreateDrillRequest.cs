namespace Api.Models.Dtos
{
    public class CreateDrillRequest
    {
        public string  Title       { get; set; } = "";
        public string  Description { get; set; } = "";
        public int     Sets        { get; set; } = 3;
        public int     Reps        { get; set; } = 10;
        public string  Duration    { get; set; } = "";
        public string  Intensity   { get; set; } = "medium";
        public string  Category    { get; set; } = "general";
        public string? VideoUrl    { get; set; }
    }
}
