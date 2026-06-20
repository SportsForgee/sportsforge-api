namespace Api.Models
{
    public class TrainingSession
    {
        public int    Id              { get; set; }
        public string CoachId         { get; set; } = "";
        public string Title           { get; set; } = "";
        public string? Notes          { get; set; }
        public DateTime ScheduledAt  { get; set; }
        public int    DurationMinutes { get; set; } = 60;
        public string Status          { get; set; } = "scheduled"; // scheduled | active | completed | cancelled
        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

        public AppUser?                    Coach        { get; set; }
        public ICollection<SessionDrill>   Drills       { get; set; } = new List<SessionDrill>();
        public ICollection<SessionParticipant> Participants { get; set; } = new List<SessionParticipant>();
    }
}
