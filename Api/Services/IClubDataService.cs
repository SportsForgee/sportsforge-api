using Api.Models;

namespace Api.Services
{
    public interface IClubDataService
    {
        ClubDashboardData GetDashboard();

        IReadOnlyList<MedicalWatchlistItem> GetMedicalWatchlist();

        MedicalWatchlistItem UpsertMedicalUpdate(CreateMedicalWatchlistUpdateRequest request);
    }
}