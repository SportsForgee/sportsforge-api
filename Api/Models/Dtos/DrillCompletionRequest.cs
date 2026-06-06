namespace Api.Models.Dtos
{
    public class DrillCompletionRequest
    {
        public int    ActualSets   { get; set; }
        public int    ActualReps   { get; set; }
        public int?   EffortRating { get; set; } // 1–10
        public string? Notes       { get; set; }
    }
}
