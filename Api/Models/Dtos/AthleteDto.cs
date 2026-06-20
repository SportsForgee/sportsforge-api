namespace Api.Models.Dtos
{
    public class AthleteDto
    {
        public string Id { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        
        public string Position { get; set; } = string.Empty;
        
        public int Performance { get; set; }
        
        public int Fitness { get; set; }
        
        public int InjuryRisk { get; set; }
        
        public string Status { get; set; } = string.Empty; // "Match Ready" | "Monitor" | "Caution"
    }
}
