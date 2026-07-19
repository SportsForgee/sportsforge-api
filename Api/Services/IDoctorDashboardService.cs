using Api.Models.Dtos;

namespace Api.Services
{
    public interface IDoctorDashboardService
    {
        Task<List<DoctorAthleteDto>> GetSquadAsync(string doctorId);
        Task<AthleteMedicalDetailDto?> GetAthleteDetailAsync(string athleteId);
        Task<bool> SetClearanceAsync(string doctorId, string athleteId, bool granted);
        Task<bool> SetNotesAsync(string doctorId, string athleteId, string notes);
    }
}
