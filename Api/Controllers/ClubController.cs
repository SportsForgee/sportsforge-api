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

        [HttpGet("squad")]
        public ActionResult<IReadOnlyList<ClubSquadPlayer>> GetSquad()
        {
            return Ok(_clubDataService.GetSquad());
        }

        [HttpGet("recruitment-shortlist")]
        public ActionResult<IReadOnlyList<RecruitmentProspect>> GetRecruitmentShortlist()
        {
            return Ok(_clubDataService.GetRecruitmentShortlist());
        }

        [HttpGet("medical-watchlist")]
        public ActionResult<IReadOnlyList<MedicalWatchlistItem>> GetMedicalWatchlist()
        {
            return Ok(_clubDataService.GetMedicalWatchlist());
        }
    }
}