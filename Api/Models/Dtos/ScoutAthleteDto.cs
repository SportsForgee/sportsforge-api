namespace Api.Models.Dtos
{
    // Deliberately excludes email, phone, medical records, injury risk and any other
    // sensitive data. Only athletes who opted in are ever projected into this shape.
    public class ScoutAthleteDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Position { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? Nationality { get; set; }
        public int? JerseyNumber { get; set; }
        public string? Team { get; set; }
        public string? Organisation { get; set; }

        // Public performance signals derived from video analysis
        public int VideoCount { get; set; }
        public double? TopSpeedKmh { get; set; }
        public double? GaitBalanceScore { get; set; }
        public double? SymmetryScore { get; set; }
        public DateTime? LastAnalysedAt { get; set; }

        // 0-100 ranking signal; null when the athlete has no analysed video yet.
        public double? AiScore { get; set; }
    }
}
