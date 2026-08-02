namespace Api.Models.Dtos
{
    public class CompletionReportDto
    {
        public int    SessionId    { get; set; }
        public string SessionTitle { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public List<AthleteCompletionDto> Athletes { get; set; } = new();
    }

    public class AthleteCompletionDto
    {
        public string AthleteId   { get; set; } = "";
        public string AthleteName { get; set; } = "";
        public int    TotalDrills { get; set; }
        public int    Completed   { get; set; }
        public List<DrillCompletionDetailDto> Drills { get; set; } = new();
    }

    public class DrillCompletionDetailDto
    {
        public int     SessionDrillId { get; set; }
        public string  DrillTitle     { get; set; } = "";
        public int     TargetSets     { get; set; }
        public int     TargetReps     { get; set; }
        public int?    ActualSets     { get; set; }
        public int?    ActualReps     { get; set; }
        public int?    EffortRating   { get; set; }
        public string? Notes          { get; set; }
        public bool    Completed      { get; set; }
        public DateTime? LoggedAt     { get; set; }
    }
}
