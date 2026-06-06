namespace Api.Models.Dtos
{
    public class SessionDto
    {
        public int      Id              { get; set; }
        public string   Title           { get; set; } = "";
        public string?  Notes           { get; set; }
        public DateTime ScheduledAt     { get; set; }
        public int      DurationMinutes { get; set; }
        public string   Status          { get; set; } = "";
        public DateTime CreatedAt       { get; set; }
        public List<SessionDrillDto>        Drills       { get; set; } = new();
        public List<SessionParticipantDto>  Participants { get; set; } = new();
    }

    public class SessionDrillDto
    {
        public int     SessionDrillId { get; set; }
        public int     DrillId        { get; set; }
        public string  Title          { get; set; } = "";
        public string  Description    { get; set; } = "";
        public int     Sets           { get; set; }
        public int     Reps           { get; set; }
        public string  Duration       { get; set; } = "";
        public string  Intensity      { get; set; } = "";
        public string  Category       { get; set; } = "";
        public string? VideoUrl       { get; set; }
        public int     Order          { get; set; }
    }

    public class SessionParticipantDto
    {
        public string AthleteId   { get; set; } = "";
        public string AthleteName { get; set; } = "";
    }
}
