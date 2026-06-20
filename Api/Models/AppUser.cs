using Microsoft.AspNetCore.Identity;

namespace Api.Models
{
    public class AppUser : IdentityUser
    {
        public string FirstName { get; set; } = "";
        public string LastName  { get; set; } = "";
        public string SfRole    { get; set; } = "";   // athlete | coach | scout | doctor | club | admin
        public string? Organisation { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Athlete profile fields
        public string? Position { get; set; }        // FW, MF, DF, GK
        public string? Height { get; set; }          // e.g., "178cm"
        public string? Weight { get; set; }          // e.g., "74kg"
        public string? Nationality { get; set; }
        public int? JerseyNumber { get; set; }
        public string? Team { get; set; }            // e.g., "Lions FC"
    }
}
