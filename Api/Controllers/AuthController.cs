using Api.Models;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly IJwtService          _jwt;

        public AuthController(UserManager<AppUser> users, IJwtService jwt)
        {
            _users = users;
            _jwt   = jwt;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (await _users.FindByEmailAsync(req.Email) != null)
                return Conflict(new { error = "An account with this email already exists." });

            var user = new AppUser
            {
                UserName     = req.Email,
                Email        = req.Email,
                FirstName    = req.FirstName,
                LastName     = req.LastName,
                SfRole       = req.Role.ToLowerInvariant(),
                Organisation = req.Organisation,
                EmailConfirmed = true,
                Position     = req.Position,
                Height       = req.Height,
                Weight       = req.Weight,
                Nationality  = req.Nationality,
                JerseyNumber = req.JerseyNumber,
                Team         = req.Team,
            };

            var result = await _users.CreateAsync(user, req.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { errors });
            }

            var token = _jwt.GenerateToken(user);

            return Ok(new AuthResponse
            {
                Token     = token,
                Email     = user.Email!,
                Name      = $"{user.FirstName} {user.LastName}",
                Role      = user.SfRole,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _users.FindByEmailAsync(req.Email);

            if (user == null || !await _users.CheckPasswordAsync(user, req.Password))
                return Unauthorized(new { error = "Invalid email or password." });

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
                return Unauthorized(new { error = "Account is temporarily locked. Please try again later." });

            var token = _jwt.GenerateToken(user);

            return Ok(new AuthResponse
            {
                Token     = token,
                Email     = user.Email!,
                Name      = $"{user.FirstName} {user.LastName}",
                Role      = user.SfRole,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            });
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

            if (userId == null) return Unauthorized();

            var user = await _users.FindByIdAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                email        = user.Email,
                name         = $"{user.FirstName} {user.LastName}",
                role         = user.SfRole,
                organisation = user.Organisation,
                createdAt    = user.CreatedAt,
            });
        }
    }
}
