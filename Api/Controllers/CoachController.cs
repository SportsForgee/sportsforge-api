using System.Security.Claims;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/coach")]
    public class CoachController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ICoachDashboardService _dashboardService;

        public CoachController(UserManager<AppUser> userManager, ICoachDashboardService dashboardService)
        {
            _userManager = userManager;
            _dashboardService = dashboardService;
        }

        // GET /api/coach/dashboard - get athletes in coach's organization
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var coach = await _userManager.FindByIdAsync(userId);
            if (coach == null)
                return NotFound("Coach not found");

            if (coach.SfRole != "coach")
                return Forbid("Only coaches can access this endpoint");

            var dashboard = await _dashboardService.GetCoachDashboardAsync(coach);
            return Ok(dashboard);
        }

        // GET /api/coach/debug - debug endpoint to see coach info and athletes in database
        [HttpGet("debug")]
        public async Task<IActionResult> GetDebugInfo()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var coach = await _userManager.FindByIdAsync(userId);
            if (coach == null)
                return NotFound("Coach not found");

            // Get all users to see what's in the database
            var allUsers = await _userManager.Users.ToListAsync();
            var athletesWithSameOrg = await _userManager.Users
                .Where(u => u.Organisation == coach.Organisation && u.SfRole == "athlete")
                .ToListAsync();

            return Ok(new
            {
                coach = new
                {
                    id = coach.Id,
                    name = $"{coach.FirstName} {coach.LastName}",
                    email = coach.Email,
                    organisation = coach.Organisation,
                    role = coach.SfRole
                },
                totalUsersInDatabase = allUsers.Count,
                totalAthletes = allUsers.Count(u => u.SfRole == "athlete"),
                athletesInCoachOrganisation = athletesWithSameOrg.Count,
                athletesInCoachOrganisationList = athletesWithSameOrg.Select(a => new
                {
                    id = a.Id,
                    name = $"{a.FirstName} {a.LastName}",
                    email = a.Email,
                    organisation = a.Organisation,
                    role = a.SfRole
                }).ToList(),
                allOrganisationsInDatabase = allUsers.Select(u => u.Organisation).Distinct().ToList(),
                allRolesInDatabase = allUsers.Select(u => u.SfRole).Distinct().ToList()
            });
        }
    }
}
