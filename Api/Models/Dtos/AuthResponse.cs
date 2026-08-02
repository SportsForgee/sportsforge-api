namespace Api.Models.Dtos
{
    public class AuthResponse
    {
        public string   Token     { get; set; } = "";
        public string   Email     { get; set; } = "";
        public string   Name      { get; set; } = "";
        public string   Role      { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}
