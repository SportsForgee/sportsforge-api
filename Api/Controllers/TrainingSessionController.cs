using System.Security.Claims;
using System.Text.Json;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/sessions")]
    public class TrainingSessionController : ControllerBase
    {
        private readonly AppDbContext          _db;
        private readonly UserManager<AppUser>  _users;
        private readonly IHubContext<MessageHub> _hub;

        public TrainingSessionController(AppDbContext db, UserManager<AppUser> users, IHubContext<MessageHub> hub)
        {
            _db    = db;
            _users = users;
            _hub   = hub;
        }

        // ── Session CRUD ──────────────────────────────────────────────────────────

        // GET /api/sessions — coach sees their sessions; athletes see sessions they're in
        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var userId = GetUserId();
            var role   = User.FindFirst("sf_role")?.Value ?? "";

            List<TrainingSession> sessions;

            if (role == "coach")
            {
                sessions = await _db.TrainingSessions
                    .Where(s => s.CoachId == userId)
                    .Include(s => s.Drills).ThenInclude(sd => sd.Drill)
                    .Include(s => s.Participants).ThenInclude(sp => sp.Athlete)
                    .OrderByDescending(s => s.ScheduledAt)
                    .ToListAsync();
            }
            else
            {
                sessions = await _db.SessionParticipants
                    .Where(sp => sp.AthleteId == userId)
                    .Include(sp => sp.Session)
                        .ThenInclude(s => s!.Drills).ThenInclude(sd => sd.Drill)
                    .Include(sp => sp.Session)
                        .ThenInclude(s => s!.Participants).ThenInclude(sp2 => sp2.Athlete)
                    .Select(sp => sp.Session!)
                    .OrderByDescending(s => s.ScheduledAt)
                    .ToListAsync();
            }

            return Ok(sessions.Select(ToDto));
        }

        // GET /api/sessions/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSession(int id)
        {
            var userId  = GetUserId();
            var session = await _db.TrainingSessions
                .Include(s => s.Drills).ThenInclude(sd => sd.Drill)
                .Include(s => s.Participants).ThenInclude(sp => sp.Athlete)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            // Coach or participant can view
            var isParticipant = session.Participants.Any(p => p.AthleteId == userId);
            if (session.CoachId != userId && !isParticipant) return Forbid();

            return Ok(ToDto(session));
        }

        // POST /api/sessions
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
        {
            var coachId = GetUserId();
            
            // Get coach's organization
            var coach = await _users.FindByIdAsync(coachId);
            if (coach == null) return Unauthorized();

            var session = new TrainingSession
            {
                CoachId         = coachId,
                Title           = req.Title,
                Notes           = req.Notes,
                ScheduledAt     = req.ScheduledAt,
                DurationMinutes = req.DurationMinutes,
            };
            _db.TrainingSessions.Add(session);
            await _db.SaveChangesAsync();

            // Attach drills in order
            for (int i = 0; i < req.DrillIds.Count; i++)
            {
                _db.SessionDrills.Add(new SessionDrill
                {
                    SessionId = session.Id,
                    DrillId   = req.DrillIds[i],
                    Order     = i + 1,
                });
            }

            // Filter participants to only include athletes from the same organization
            var validAthleteIds = new List<string>();
            foreach (var athleteId in req.AthleteIds.Distinct())
            {
                var athlete = await _users.FindByIdAsync(athleteId);
                if (athlete != null && 
                    athlete.SfRole == "athlete" && 
                    athlete.Organisation == coach.Organisation)
                {
                    validAthleteIds.Add(athleteId);
                }
            }

            // Attach only valid participants
            foreach (var athleteId in validAthleteIds)
                _db.SessionParticipants.Add(new SessionParticipant
                {
                    SessionId = session.Id,
                    AthleteId = athleteId,
                });

            await _db.SaveChangesAsync();

            // Reload full session
            var full = await _db.TrainingSessions
                .Include(s => s.Drills).ThenInclude(sd => sd.Drill)
                .Include(s => s.Participants).ThenInclude(sp => sp.Athlete)
                .FirstAsync(s => s.Id == session.Id);

            return Ok(ToDto(full));
        }

        // DELETE /api/sessions/{id} — cancel
        [HttpDelete("{id}")]
        public async Task<IActionResult> Cancel(int id)
        {
            var session = await _db.TrainingSessions.FindAsync(id);
            if (session == null || session.CoachId != GetUserId()) return NotFound();
            session.Status = "cancelled";
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ── Dispatch ──────────────────────────────────────────────────────────────

        // POST /api/sessions/{id}/dispatch — send all drill cards to all participants via SignalR
        [HttpPost("{id}/dispatch")]
        public async Task<IActionResult> Dispatch(int id)
        {
            var coachId = GetUserId();
            var session = await _db.TrainingSessions
                .Include(s => s.Drills).ThenInclude(sd => sd.Drill)
                .Include(s => s.Participants)
                .FirstOrDefaultAsync(s => s.Id == id && s.CoachId == coachId);

            if (session == null) return NotFound();

            var coach     = await _users.FindByIdAsync(coachId);
            var coachName = coach != null ? $"{coach.FirstName} {coach.LastName}" : "Coach";

            int count = 0;

            foreach (var participant in session.Participants)
            {
                foreach (var sd in session.Drills.OrderBy(d => d.Order))
                {
                    var drill = sd.Drill!;
                    var card  = new DrillCard
                    {
                        SessionDrillId = sd.Id,
                        Title          = drill.Title,
                        Description    = drill.Description,
                        Sets           = drill.Sets,
                        Reps           = drill.Reps,
                        Duration       = drill.Duration,
                        Intensity      = drill.Intensity,
                        Category       = drill.Category,
                        VideoUrl       = drill.VideoUrl,
                    };

                    var msg = new Message
                    {
                        SenderId   = coachId,
                        SenderName = coachName,
                        ReceiverId = participant.AthleteId,
                        Content    = drill.Title,
                        Type       = "drill",
                        Metadata   = JsonSerializer.Serialize(card),
                    };
                    _db.Messages.Add(msg);
                    await _db.SaveChangesAsync();

                    await _hub.Clients
                        .Group($"user-{participant.AthleteId}")
                        .SendAsync("NewMessage", new MessageDto
                        {
                            Id         = msg.Id,
                            SenderId   = msg.SenderId,
                            SenderName = msg.SenderName,
                            ReceiverId = msg.ReceiverId,
                            Content    = msg.Content,
                            Type       = msg.Type,
                            Metadata   = msg.Metadata,
                            SentAt     = msg.SentAt,
                        });

                    count++;
                }
            }

            session.Status = "active";
            await _db.SaveChangesAsync();

            return Ok(new { dispatched = count });
        }

        // ── Completion logging ────────────────────────────────────────────────────

        // POST /api/sessions/drills/{sessionDrillId}/complete — athlete logs their performance
        [HttpPost("drills/{sessionDrillId}/complete")]
        public async Task<IActionResult> LogCompletion(int sessionDrillId, [FromBody] DrillCompletionRequest req)
        {
            var athleteId = GetUserId();

            // Verify athlete is a participant
            var sessionDrill = await _db.SessionDrills
                .Include(sd => sd.Session)
                .FirstOrDefaultAsync(sd => sd.Id == sessionDrillId);

            if (sessionDrill == null) return NotFound();

            var isParticipant = await _db.SessionParticipants
                .AnyAsync(sp => sp.SessionId == sessionDrill.SessionId && sp.AthleteId == athleteId);

            if (!isParticipant) return Forbid();

            // Upsert — athlete can re-submit (overwrite previous log)
            var existing = await _db.DrillCompletions
                .FirstOrDefaultAsync(dc => dc.SessionDrillId == sessionDrillId && dc.AthleteId == athleteId);

            if (existing != null)
            {
                existing.ActualSets   = req.ActualSets;
                existing.ActualReps   = req.ActualReps;
                existing.EffortRating = req.EffortRating;
                existing.Notes        = req.Notes;
                existing.LoggedAt     = DateTime.UtcNow;
            }
            else
            {
                _db.DrillCompletions.Add(new DrillCompletion
                {
                    SessionDrillId = sessionDrillId,
                    AthleteId      = athleteId,
                    ActualSets     = req.ActualSets,
                    ActualReps     = req.ActualReps,
                    EffortRating   = req.EffortRating,
                    Notes          = req.Notes,
                });
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // GET /api/sessions/{id}/report — coach views completion report
        [HttpGet("{id}/report")]
        public async Task<IActionResult> GetReport(int id)
        {
            var coachId = GetUserId();
            var session = await _db.TrainingSessions
                .Include(s => s.Drills).ThenInclude(sd => sd.Drill)
                .Include(s => s.Participants).ThenInclude(sp => sp.Athlete)
                .Include(s => s.Drills).ThenInclude(sd => sd.Completions)
                .FirstOrDefaultAsync(s => s.Id == id && s.CoachId == coachId);

            if (session == null) return NotFound();

            var report = new CompletionReportDto
            {
                SessionId    = session.Id,
                SessionTitle = session.Title,
                ScheduledAt  = session.ScheduledAt,
                Athletes     = session.Participants.Select(p =>
                {
                    var drillDetails = session.Drills.OrderBy(sd => sd.Order).Select(sd =>
                    {
                        var completion = sd.Completions.FirstOrDefault(c => c.AthleteId == p.AthleteId);
                        return new DrillCompletionDetailDto
                        {
                            SessionDrillId = sd.Id,
                            DrillTitle     = sd.Drill!.Title,
                            TargetSets     = sd.Drill.Sets,
                            TargetReps     = sd.Drill.Reps,
                            ActualSets     = completion?.ActualSets,
                            ActualReps     = completion?.ActualReps,
                            EffortRating   = completion?.EffortRating,
                            Notes          = completion?.Notes,
                            Completed      = completion?.Completed ?? false,
                            LoggedAt       = completion?.LoggedAt,
                        };
                    }).ToList();

                    return new AthleteCompletionDto
                    {
                        AthleteId   = p.AthleteId,
                        AthleteName = p.Athlete != null ? $"{p.Athlete.FirstName} {p.Athlete.LastName}" : p.AthleteId,
                        TotalDrills = session.Drills.Count,
                        Completed   = drillDetails.Count(d => d.Completed),
                        Drills      = drillDetails,
                    };
                }).ToList(),
            };

            return Ok(report);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string GetUserId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException();

        private static SessionDto ToDto(TrainingSession s) => new()
        {
            Id              = s.Id,
            Title           = s.Title,
            Notes           = s.Notes,
            ScheduledAt     = s.ScheduledAt,
            DurationMinutes = s.DurationMinutes,
            Status          = s.Status,
            CreatedAt       = s.CreatedAt,
            Drills = s.Drills.OrderBy(sd => sd.Order).Select(sd => new SessionDrillDto
            {
                SessionDrillId = sd.Id,
                DrillId        = sd.DrillId,
                Title          = sd.Drill?.Title       ?? "",
                Description    = sd.Drill?.Description ?? "",
                Sets           = sd.Drill?.Sets        ?? 0,
                Reps           = sd.Drill?.Reps        ?? 0,
                Duration       = sd.Drill?.Duration    ?? "",
                Intensity      = sd.Drill?.Intensity   ?? "",
                Category       = sd.Drill?.Category    ?? "",
                VideoUrl       = sd.Drill?.VideoUrl,
                Order          = sd.Order,
            }).ToList(),
            Participants = s.Participants.Select(p => new SessionParticipantDto
            {
                AthleteId   = p.AthleteId,
                AthleteName = p.Athlete != null ? $"{p.Athlete.FirstName} {p.Athlete.LastName}" : p.AthleteId,
            }).ToList(),
        };
    }
}
