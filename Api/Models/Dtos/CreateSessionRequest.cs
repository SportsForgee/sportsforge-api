namespace Api.Models.Dtos
{
    public class CreateSessionRequest
    {
        public string        Title           { get; set; } = "";
        public string?       Notes           { get; set; }
        public DateTime      ScheduledAt     { get; set; }
        public int           DurationMinutes { get; set; } = 60;
        public List<int>     DrillIds        { get; set; } = new();  // ordered list of drill IDs
        public List<string>  AthleteIds      { get; set; } = new();
    }
}
