using Api.Models;

namespace Api.Services
{
    public class InMemoryClubDataService : IClubDataService
    {
        private readonly List<ClubSquadPlayer> _squad =
        [
            new ClubSquadPlayer
            {
                Id = 1,
                Name = "M. Al-Hakim",
                Position = "ST",
                Age = 24,
                Form = 88,
                Value = "$12.8M",
                Availability = "Available",
            },
            new ClubSquadPlayer
            {
                Id = 2,
                Name = "J. Mensah",
                Position = "CM",
                Age = 27,
                Form = 81,
                Value = "$9.5M",
                Availability = "Monitor",
            },
            new ClubSquadPlayer
            {
                Id = 3,
                Name = "R. Duarte",
                Position = "CB",
                Age = 29,
                Form = 79,
                Value = "$8.1M",
                Availability = "Available",
            },
            new ClubSquadPlayer
            {
                Id = 4,
                Name = "L. Nakanishi",
                Position = "RW",
                Age = 22,
                Form = 84,
                Value = "$14.2M",
                Availability = "Injured",
            },
            new ClubSquadPlayer
            {
                Id = 5,
                Name = "T. Diallo",
                Position = "LB",
                Age = 25,
                Form = 76,
                Value = "$7.6M",
                Availability = "Available",
            },
        ];

        private readonly List<RecruitmentProspect> _recruitmentShortlist =
        [
            new RecruitmentProspect
            {
                Name = "K. Petrov",
                Position = "AM",
                Club = "Sofia United",
                Score = 91,
                MarketValue = "$5.2M",
            },
            new RecruitmentProspect
            {
                Name = "A. Bako",
                Position = "CB",
                Club = "Lagos Stars",
                Score = 87,
                MarketValue = "$4.1M",
            },
            new RecruitmentProspect
            {
                Name = "N. Salim",
                Position = "RW",
                Club = "Cairo FC",
                Score = 85,
                MarketValue = "$6.3M",
            },
        ];

        private readonly List<MedicalWatchlistItem> _medicalWatchlist =
        [
            new MedicalWatchlistItem
            {
                Id = 1,
                PlayerName = "L. Nakanishi",
                Issue = "Hamstring strain",
                Status = "Return in 9 days",
                Priority = "High",
            },
            new MedicalWatchlistItem
            {
                Id = 2,
                PlayerName = "Y. Ghanem",
                Issue = "Load management",
                Status = "72% training intensity",
                Priority = "Medium",
            },
            new MedicalWatchlistItem
            {
                Id = 3,
                PlayerName = "D. Silva",
                Issue = "Ankle recovery",
                Status = "Clearance review tomorrow",
                Priority = "Low",
            },
        ];

        private readonly List<FixtureSummary> _upcomingFixtures =
        [
            new FixtureSummary
            {
                MatchTitle = "SportsForge FC vs Delta City",
                Kickoff = "May 11, 19:30",
            },
            new FixtureSummary
            {
                MatchTitle = "Northbridge United vs SportsForge FC",
                Kickoff = "May 15, 18:00",
            },
            new FixtureSummary
            {
                MatchTitle = "SportsForge FC vs Azure Town",
                Kickoff = "May 21, 20:00",
            },
        ];

        public ClubDashboardData GetDashboard()
        {
            var watchlist = GetMedicalWatchlist().ToList();
            var healthyPlayers = Math.Max(0, 27 - watchlist.Count - 1);
            var readinessPenalty = watchlist.Sum(GetPenaltyForPriority);

            return new ClubDashboardData
            {
                TotalPlayers = 27,
                HealthyPlayers = healthyPlayers,
                PlayersInRecovery = watchlist.Count,
                MedicalReadinessPercentage = Math.Max(0, 100 - readinessPenalty),
                RecentFormChangePercentage = 11,
                PendingReports = 5,
                SquadValue = "$96.4M",
                SquadValueDelta = "+4.3% since last month",
                ScoutingOpportunities = 14,
                HighPriorityMatches = 5,
                BudgetRemaining = "$18.7M",
                TransferWindowDaysLeft = 41,
                UpcomingFixtures = _upcomingFixtures
                    .Select(fixture => new FixtureSummary
                    {
                        MatchTitle = fixture.MatchTitle,
                        Kickoff = fixture.Kickoff,
                    })
                    .ToList(),
                MedicalWatchlist = watchlist,
            };
        }

        public IReadOnlyList<ClubSquadPlayer> GetSquad()
        {
            return _squad
                .Select(player => new ClubSquadPlayer
                {
                    Id = player.Id,
                    Name = player.Name,
                    Position = player.Position,
                    Age = player.Age,
                    Form = player.Form,
                    Value = player.Value,
                    Availability = player.Availability,
                })
                .ToList();
        }

        public IReadOnlyList<RecruitmentProspect> GetRecruitmentShortlist()
        {
            return _recruitmentShortlist
                .OrderByDescending(prospect => prospect.Score)
                .Select(prospect => new RecruitmentProspect
                {
                    Name = prospect.Name,
                    Position = prospect.Position,
                    Club = prospect.Club,
                    Score = prospect.Score,
                    MarketValue = prospect.MarketValue,
                })
                .ToList();
        }

        public IReadOnlyList<MedicalWatchlistItem> GetMedicalWatchlist()
        {
            return _medicalWatchlist
                .OrderBy(item => GetPenaltyForPriority(item))
                .Select(item => new MedicalWatchlistItem
                {
                    Id = item.Id,
                    PlayerName = item.PlayerName,
                    Issue = item.Issue,
                    Status = item.Status,
                    Priority = item.Priority,
                })
                .ToList();
        }

        public MedicalWatchlistItem UpsertMedicalUpdate(CreateMedicalWatchlistUpdateRequest request)
        {
            var existingItem = _medicalWatchlist.FirstOrDefault(item =>
                string.Equals(item.PlayerName, request.PlayerName, StringComparison.OrdinalIgnoreCase));

            if (existingItem is null)
            {
                existingItem = new MedicalWatchlistItem
                {
                    Id = _medicalWatchlist.Count == 0 ? 1 : _medicalWatchlist.Max(item => item.Id) + 1,
                    PlayerName = request.PlayerName.Trim(),
                };

                _medicalWatchlist.Add(existingItem);
            }

            existingItem.Issue = request.Issue.Trim();
            existingItem.Status = request.Status.Trim();
            existingItem.Priority = request.Priority.Trim();

            return new MedicalWatchlistItem
            {
                Id = existingItem.Id,
                PlayerName = existingItem.PlayerName,
                Issue = existingItem.Issue,
                Status = existingItem.Status,
                Priority = existingItem.Priority,
            };
        }

        private static int GetPenaltyForPriority(MedicalWatchlistItem item)
        {
            return item.Priority.ToLowerInvariant() switch
            {
                "high" => 6,
                "medium" => 4,
                _ => 2,
            };
        }
    }
}