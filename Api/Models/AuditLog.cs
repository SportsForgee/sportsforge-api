namespace Api.Models
{
    public class AuditLog
    {
        public int      Id         { get; set; }
        public string?  UserId     { get; set; }
        public string?  UserEmail  { get; set; }
        public string?  UserRole   { get; set; }

        public string   Category   { get; set; } = "";  // auth | admin | system
        public string   Action     { get; set; } = "";  // login.success | user.suspended | ...
        public string?  Details    { get; set; }
        public bool     Success    { get; set; } = true;

        public string?  IpAddress  { get; set; }
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    }
}
