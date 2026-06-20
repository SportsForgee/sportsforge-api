namespace Api.Models.Dtos
{
    public class AthleteProfileDto
    {
        public string Id { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Position { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? Nationality { get; set; }
        public int? JerseyNumber { get; set; }
        public string? Team { get; set; }
        public string? Organisation { get; set; }
    }
}
