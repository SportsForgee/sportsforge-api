using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/athlete")]
    [Authorize]
    public class AthleteController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;

        public AthleteController(UserManager<AppUser> users)
        {
            _users = users;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

            if (userId == null) return Unauthorized(new { error = "Not authenticated." });

            var user = await _users.FindByIdAsync(userId);
            if (user == null) return NotFound(new { error = "User not found." });

            return Ok(new AthleteProfileDto
            {
                Id           = user.Id,
                FirstName    = user.FirstName,
                LastName     = user.LastName,
                Position     = user.Position,
                Height       = user.Height,
                Weight       = user.Weight,
                Nationality  = user.Nationality,
                JerseyNumber = user.JerseyNumber,
                Team         = user.Team,
                Organisation = user.Organisation,
                IsVisibleToScouts = user.IsVisibleToScouts,
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] AthleteProfileUpdateRequest req)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

            if (userId == null) return Unauthorized(new { error = "Not authenticated." });

            var user = await _users.FindByIdAsync(userId);
            if (user == null) return NotFound(new { error = "User not found." });

            user.Position     = req.Position ?? user.Position;
            user.Height       = req.Height ?? user.Height;
            user.Weight       = req.Weight ?? user.Weight;
            user.Nationality  = req.Nationality ?? user.Nationality;
            user.JerseyNumber = req.JerseyNumber ?? user.JerseyNumber;
            user.Team         = req.Team ?? user.Team;
            user.IsVisibleToScouts = req.IsVisibleToScouts ?? user.IsVisibleToScouts;

            var result = await _users.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            return Ok(new AthleteProfileDto
            {
                Id           = user.Id,
                FirstName    = user.FirstName,
                LastName     = user.LastName,
                Position     = user.Position,
                Height       = user.Height,
                Weight       = user.Weight,
                Nationality  = user.Nationality,
                JerseyNumber = user.JerseyNumber,
                Team         = user.Team,
                Organisation = user.Organisation,
                IsVisibleToScouts = user.IsVisibleToScouts,
            });
        }
    }
}
