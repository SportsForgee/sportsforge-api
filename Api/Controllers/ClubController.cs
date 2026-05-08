using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/club")]
    public class ClubController : ControllerBase
    {
        private readonly IClubDataService _clubDataService;

        public ClubController(IClubDataService clubDataService)
        {
            _clubDataService = clubDataService;
        }

        [HttpGet("dashboard")]
        public ActionResult<ClubDashboardData> GetDashboard()
        {
            return Ok(_clubDataService.GetDashboard());
        }

        [HttpGet("medical-watchlist")]
        public ActionResult<IReadOnlyList<MedicalWatchlistItem>> GetMedicalWatchlist()
        {
            return Ok(_clubDataService.GetMedicalWatchlist());
        }
    }
}