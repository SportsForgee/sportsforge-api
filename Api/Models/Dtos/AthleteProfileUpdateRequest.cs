namespace Api.Models.Dtos
{
    public class AthleteProfileUpdateRequest
    {
        public string? Position { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? Nationality { get; set; }
        public int? JerseyNumber { get; set; }
        public string? Team { get; set; }
    }
}
