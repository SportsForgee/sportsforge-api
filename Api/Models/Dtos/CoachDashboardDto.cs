namespace Api.Models.Dtos
{
    public class CoachDashboardDto
    {
        public string CoachName { get; set; } = string.Empty;
        
        public string Organisation { get; set; } = string.Empty;
        
        public IReadOnlyList<AthleteDto> Athletes { get; set; } = new List<AthleteDto>();
    }
}
