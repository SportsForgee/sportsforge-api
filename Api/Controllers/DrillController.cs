using System.Security.Claims;
using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/drills")]
    public class DrillController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DrillController(AppDbContext db) => _db = db;

        // GET /api/drills — coach's own drill library
        [HttpGet]
        public async Task<IActionResult> GetDrills()
        {
            var coachId = GetUserId();
            var drills  = await _db.Drills
                .Where(d => d.CoachId == coachId && d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
            return Ok(drills.Select(ToDto));
        }

        // GET /api/drills/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDrill(int id)
        {
            var drill = await _db.Drills.FindAsync(id);
            if (drill == null || drill.CoachId != GetUserId()) return NotFound();
            return Ok(ToDto(drill));
        }

        // POST /api/drills
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDrillRequest req)
        {
            var drill = new Drill
            {
                CoachId     = GetUserId(),
                Title       = req.Title,
                Description = req.Description,
                Sets        = req.Sets,
                Reps        = req.Reps,
                Duration    = req.Duration,
                Intensity   = req.Intensity,
                Category    = req.Category,
                VideoUrl    = req.VideoUrl,
            };
            _db.Drills.Add(drill);
            await _db.SaveChangesAsync();
            return Ok(ToDto(drill));
        }

        // PUT /api/drills/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateDrillRequest req)
        {
            var drill = await _db.Drills.FindAsync(id);
            if (drill == null || drill.CoachId != GetUserId()) return NotFound();

            drill.Title       = req.Title;
            drill.Description = req.Description;
            drill.Sets        = req.Sets;
            drill.Reps        = req.Reps;
            drill.Duration    = req.Duration;
            drill.Intensity   = req.Intensity;
            drill.Category    = req.Category;
            drill.VideoUrl    = req.VideoUrl;

            await _db.SaveChangesAsync();
            return Ok(ToDto(drill));
        }

        // DELETE /api/drills/{id} — soft delete
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var drill = await _db.Drills.FindAsync(id);
            if (drill == null || drill.CoachId != GetUserId()) return NotFound();
            drill.IsActive = false;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();

        private static DrillDto ToDto(Drill d) => new()
        {
            Id          = d.Id,
            Title       = d.Title,
            Description = d.Description,
            Sets        = d.Sets,
            Reps        = d.Reps,
            Duration    = d.Duration,
            Intensity   = d.Intensity,
            Category    = d.Category,
            VideoUrl    = d.VideoUrl,
            CreatedAt   = d.CreatedAt,
        };
    }
}
