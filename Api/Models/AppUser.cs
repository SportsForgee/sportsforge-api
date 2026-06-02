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
    }
}
