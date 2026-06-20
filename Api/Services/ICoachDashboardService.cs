using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Api.Services
{
    public interface ICoachDashboardService
    {
        Task<CoachDashboardDto> GetCoachDashboardAsync(AppUser coach);
    }

    public class CoachDashboardService : ICoachDashboardService
    {
        private readonly AppDbContext _db;
        private readonly Random _random;

        public CoachDashboardService(AppDbContext db)
        {
            _db = db;
            _random = new Random();
        }

        public async Task<CoachDashboardDto> GetCoachDashboardAsync(AppUser coach)
        {
            if (string.IsNullOrEmpty(coach.Organisation))
                return new CoachDashboardDto 
                { 
                    CoachName = $"{coach.FirstName} {coach.LastName}",
                    Organisation = "Unknown",
                    Athletes = new List<AthleteDto>()
                };

            // Fetch all athletes in the same organization
            var athletes = await _db.Users
                .Where(u => u.Organisation == coach.Organisation && u.SfRole == "athlete")
                .ToListAsync();

            // Generate stats for each athlete
            var athleteDtos = athletes.Select(a => GenerateAthleteStats(a)).ToList();

            return new CoachDashboardDto
            {
                CoachName = $"{coach.FirstName} {coach.LastName}",
                Organisation = coach.Organisation,
                Athletes = athleteDtos,
            };
        }

        private AthleteDto GenerateAthleteStats(AppUser athlete)
        {
            // Deterministically generate stats based on athlete ID hash
            var seed = athlete.Id.GetHashCode();
            var tempRandom = new Random(seed);

            var performance = tempRandom.Next(70, 96);
            var fitness = tempRandom.Next(65, 96);
            var injuryRisk = tempRandom.Next(5, 50);

            var status = injuryRisk switch
            {
                > 40 => "Monitor",
                > 25 => "Caution",
                _ => "Match Ready",
            };

            var positions = new[] { "FW", "MF", "DF", "GK" };
            var position = positions[tempRandom.Next(positions.Length)];

            return new AthleteDto
            {
                Id = athlete.Id,
                Name = $"{athlete.FirstName} {athlete.LastName}",
                Position = !string.IsNullOrEmpty(athlete.Position) ? athlete.Position : position,
                Performance = performance,
                Fitness = fitness,
                InjuryRisk = injuryRisk,
                Status = status,
            };
        }
    }
}
