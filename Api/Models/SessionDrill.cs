namespace Api.Models
{
    public class SessionDrill
    {
        public int Id        { get; set; }
        public int SessionId { get; set; }
        public int DrillId   { get; set; }
        public int Order     { get; set; }

        public TrainingSession? Session    { get; set; }
        public Drill?           Drill      { get; set; }
        public ICollection<DrillCompletion> Completions { get; set; } = new List<DrillCompletion>();
    }
}
