using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/scout")]
    public class ScoutController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly AppDbContext _db;

        public ScoutController(UserManager<AppUser> users, AppDbContext db)
        {
            _users = users;
            _db = db;
        }

        // GET /api/scout/athletes — athletes who opted in to scout visibility
        [HttpGet("athletes")]
        public async Task<IActionResult> GetVisibleAthletes([FromQuery] string? position, [FromQuery] string? search)
        {
            var scout = await GetScoutAsync();
            if (scout == null) return Forbid();

            var query = _users.Users.Where(u => u.SfRole == "athlete" && u.IsVisibleToScouts);

            if (!string.IsNullOrWhiteSpace(position))
                query = query.Where(u => u.Position == position);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    u.FirstName.Contains(term) ||
                    u.LastName.Contains(term) ||
                    (u.Team != null && u.Team.Contains(term)) ||
                    (u.Nationality != null && u.Nationality.Contains(term)));
            }

            var athletes = await query
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

            var ids = athletes.Select(a => a.Id).ToList();
            var stats = await GetVideoStatsAsync(ids);

            return Ok(athletes.Select(a => ToDto(a, stats.GetValueOrDefault(a.Id))));
        }

        // GET /api/scout/athletes/{athleteId} — single opted-in athlete
        [HttpGet("athletes/{athleteId}")]
        public async Task<IActionResult> GetVisibleAthlete(string athleteId)
        {
            var scout = await GetScoutAsync();
            if (scout == null) return Forbid();

            var athlete = await _users.Users
                .FirstOrDefaultAsync(u => u.Id == athleteId && u.SfRole == "athlete" && u.IsVisibleToScouts);

            // Same response whether the athlete does not exist or is hidden, so scouts
            // cannot probe for the existence of opted-out athletes.
            if (athlete == null) return NotFound(new { error = "Athlete not found." });

            var stats = await GetVideoStatsAsync(new List<string> { athlete.Id });
            return Ok(ToDto(athlete, stats.GetValueOrDefault(athlete.Id)));
        }

        private async Task<AppUser?> GetScoutAsync()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var user = await _users.FindByIdAsync(userId);
            return user?.SfRole == "scout" ? user : null;
        }

        private record VideoStats(int VideoCount, double? TopSpeedKmh, double? GaitBalanceScore, double? SymmetryScore, DateTime? LastAnalysedAt);

        private async Task<Dictionary<string, VideoStats>> GetVideoStatsAsync(List<string> athleteIds)
        {
            if (athleteIds.Count == 0) return new Dictionary<string, VideoStats>();

            var rows = await _db.VideoUploads
                .Where(v => athleteIds.Contains(v.AthleteId))
                .GroupJoin(_db.VideoAnalysisResults,
                    v => v.Id,
                    r => r.VideoUploadId,
                    (v, results) => new { v.AthleteId, Result = results.FirstOrDefault() })
                .ToListAsync();

            return rows
                .GroupBy(x => x.AthleteId)
                .ToDictionary(g => g.Key, g => new VideoStats(
                    g.Count(),
                    g.Max(x => x.Result != null ? x.Result.TopSpeedKmh : null),
                    g.Average(x => x.Result != null ? x.Result.GaitBalanceScore : null),
                    g.Average(x => x.Result != null ? x.Result.SymmetryScore : null),
                    g.Max(x => x.Result != null ? (DateTime?)x.Result.CreatedAt : null)));
        }

        private static ScoutAthleteDto ToDto(AppUser a, VideoStats? stats) => new()
        {
            Id               = a.Id,
            Name             = $"{a.FirstName} {a.LastName}".Trim(),
            Position         = a.Position,
            Height           = a.Height,
            Weight           = a.Weight,
            Nationality      = a.Nationality,
            JerseyNumber     = a.JerseyNumber,
            Team             = a.Team,
            Organisation     = a.Organisation,
            VideoCount       = stats?.VideoCount ?? 0,
            TopSpeedKmh      = stats?.TopSpeedKmh,
            GaitBalanceScore = stats?.GaitBalanceScore,
            SymmetryScore    = stats?.SymmetryScore,
            LastAnalysedAt   = stats?.LastAnalysedAt,
            AiScore          = CalculateAiScore(stats),
        };

        // Weighted blend of whichever video metrics exist; 36 km/h is treated as an elite sprint ceiling.
        private static double? CalculateAiScore(VideoStats? stats)
        {
            if (stats == null) return null;

            var parts = new List<(double Value, double Weight)>();

            if (stats.TopSpeedKmh is double speed)
                parts.Add((Math.Clamp(speed / 36d * 100d, 0, 100), 0.4));

            if (stats.GaitBalanceScore is double balance)
                parts.Add((Math.Clamp(balance, 0, 100), 0.35));

            if (stats.SymmetryScore is double symmetry)
                parts.Add((Math.Clamp(symmetry, 0, 100), 0.25));

            if (parts.Count == 0) return null;

            var totalWeight = parts.Sum(p => p.Weight);
            return Math.Round(parts.Sum(p => p.Value * p.Weight) / totalWeight, 1);
        }
    }
}
