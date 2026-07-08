using Api.Models;

namespace Api.Services
{
    public interface IClubDataService
    {
        ClubDashboardData GetDashboard();

        IReadOnlyList<ClubSquadPlayer> GetSquad();

        IReadOnlyList<RecruitmentProspect> GetRecruitmentShortlist();

        IReadOnlyList<MedicalWatchlistItem> GetMedicalWatchlist();

        MedicalWatchlistItem UpsertMedicalUpdate(CreateMedicalWatchlistUpdateRequest request);
    }
}