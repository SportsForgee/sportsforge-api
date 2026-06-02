using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(AppUser user)
        {
            var secret  = _config["Jwt:Secret"]!;
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddDays(7);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,             user.Id),
                new Claim(JwtRegisteredClaimNames.Email,           user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.GivenName,       user.FirstName),
                new Claim(JwtRegisteredClaimNames.FamilyName,      user.LastName),
                new Claim("sf_role",                               user.SfRole),
                new Claim(JwtRegisteredClaimNames.Jti,             Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer:             _config["Jwt:Issuer"],
                audience:           _config["Jwt:Audience"],
                claims:             claims,
                expires:            expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
