namespace Api.Models.Dtos
{
    public class DrillCard
    {
        public int?    SessionDrillId { get; set; }  // set when dispatched from a session
        public string  Title          { get; set; } = "";
        public string  Description    { get; set; } = "";
        public int     Sets           { get; set; }
        public int     Reps           { get; set; }
        public string  Duration       { get; set; } = "";
        public string  Intensity      { get; set; } = "medium"; // low | medium | high
        public string  Category       { get; set; } = "general";
        public string? VideoUrl       { get; set; }
    }
}
