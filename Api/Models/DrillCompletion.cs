namespace Api.Models
{
    public class DrillCompletion
    {
        public int    Id             { get; set; }
        public int    SessionDrillId { get; set; }
        public string AthleteId      { get; set; } = "";
        public int    ActualSets     { get; set; }
        public int    ActualReps     { get; set; }
        public int?   EffortRating   { get; set; } // 1–10
        public string? Notes         { get; set; }
        public bool   Completed      { get; set; } = true;
        public DateTime LoggedAt    { get; set; } = DateTime.UtcNow;

        public SessionDrill? SessionDrill { get; set; }
        public AppUser?      Athlete      { get; set; }
    }
}
