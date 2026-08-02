namespace Api.Models
{
    public class RecruitmentProspect
    {
        public string Name { get; set; } = string.Empty;

        public string Position { get; set; } = string.Empty;

        public string Club { get; set; } = string.Empty;

        public int Score { get; set; }

        public string MarketValue { get; set; } = string.Empty;
    }
}