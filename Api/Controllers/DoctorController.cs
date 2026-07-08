using System.Security.Claims;
using Api.Models.Dtos;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/doctor")]
    public class DoctorController : ControllerBase
    {
        private readonly IDoctorDashboardService _doctor;

        public DoctorController(IDoctorDashboardService doctor) => _doctor = doctor;

        // GET /api/doctor/athletes — squad list with injury risk, status, device state
        [HttpGet("athletes")]
        public async Task<IActionResult> GetAthletes()
        {
            var squad = await _doctor.GetSquadAsync(GetUserId());
            return Ok(squad);
        }

        // GET /api/doctor/athletes/{athleteId} — full medical detail view
        [HttpGet("athletes/{athleteId}")]
        public async Task<IActionResult> GetAthlete(string athleteId)
        {
            var detail = await _doctor.GetAthleteDetailAsync(athleteId);
            return detail == null ? NotFound() : Ok(detail);
        }

        // PUT /api/doctor/athletes/{athleteId}/clearance
        [HttpPut("athletes/{athleteId}/clearance")]
        public async Task<IActionResult> SetClearance(string athleteId, [FromBody] SetClearanceRequest req)
        {
            var ok = await _doctor.SetClearanceAsync(GetUserId(), athleteId, req.Granted);
            return ok ? NoContent() : NotFound();
        }

        // PUT /api/doctor/athletes/{athleteId}/notes
        [HttpPut("athletes/{athleteId}/notes")]
        public async Task<IActionResult> SetNotes(string athleteId, [FromBody] SetNotesRequest req)
        {
            var ok = await _doctor.SetNotesAsync(GetUserId(), athleteId, req.Notes);
            return ok ? NoContent() : NotFound();
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();
    }
}
