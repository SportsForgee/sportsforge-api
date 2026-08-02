namespace Api.Models
{
    public class SessionParticipant
    {
        public int    Id        { get; set; }
        public int    SessionId { get; set; }
        public string AthleteId { get; set; } = "";

        public TrainingSession? Session { get; set; }
        public AppUser?         Athlete { get; set; }
    }
}
