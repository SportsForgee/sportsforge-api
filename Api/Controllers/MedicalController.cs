using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/medical")]
    public class MedicalController : ControllerBase
    {
        private readonly IClubDataService _clubDataService;

        public MedicalController(IClubDataService clubDataService)
        {
            _clubDataService = clubDataService;
        }

        [HttpPost("club-update")]
        public ActionResult<MedicalWatchlistItem> SendClubMedicalUpdate([FromBody] CreateMedicalWatchlistUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlayerName) ||
                string.IsNullOrWhiteSpace(request.Issue) ||
                string.IsNullOrWhiteSpace(request.Status) ||
                string.IsNullOrWhiteSpace(request.Priority))
            {
                return BadRequest("PlayerName, Issue, Status, and Priority are required.");
            }

            var updatedItem = _clubDataService.UpsertMedicalUpdate(request);
            return Ok(updatedItem);
        }
    }
}