using Api.Models;

namespace Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(AppUser user);
    }
}
