namespace Api.Models.Dtos
{
    public class RegisterRequest
    {
        public string FirstName    { get; set; } = "";
        public string LastName     { get; set; } = "";
        public string Email        { get; set; } = "";
        public string Password     { get; set; } = "";
        public string Role         { get; set; } = "";
        public string? Organisation { get; set; }

        // Athlete profile fields (optional, only for athletes)
        public string? Position { get; set; }
        public string? Height { get; set; }
        public string? Weight { get; set; }
        public string? Nationality { get; set; }
        public int? JerseyNumber { get; set; }
        public string? Team { get; set; }
    }
}
