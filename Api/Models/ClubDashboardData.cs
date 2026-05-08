namespace Api.Models
{
    public class ClubDashboardData
    {
        public int TotalPlayers { get; set; }

        public int HealthyPlayers { get; set; }

        public int PlayersInRecovery { get; set; }

        public int MedicalReadinessPercentage { get; set; }

        public int RecentFormChangePercentage { get; set; }

        public int PendingReports { get; set; }

        public string SquadValue { get; set; } = string.Empty;

        public string SquadValueDelta { get; set; } = string.Empty;

        public int ScoutingOpportunities { get; set; }

        public int HighPriorityMatches { get; set; }

        public string BudgetRemaining { get; set; } = string.Empty;

        public int TransferWindowDaysLeft { get; set; }

        public List<FixtureSummary> UpcomingFixtures { get; set; } = [];

        public List<MedicalWatchlistItem> MedicalWatchlist { get; set; } = [];
    }
}