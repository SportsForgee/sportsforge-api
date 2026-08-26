using Api.Models;
using Api.Models.Dtos;

namespace Api.Services
{
    public interface IVideoAnalysisService
    {
        Task<VideoUpload> SaveUploadAsync(string athleteId, string uploadedByUserId, string fileName, long sizeBytes, Stream content, CancellationToken ct = default);
        Task<bool> StartAnalysisAsync(VideoUpload upload, CancellationToken ct = default);
        Task<VideoAnalysisResult> HandleCallbackAsync(VideoAnalysisCallbackRequest payload, CancellationToken ct = default);
        Task<List<VideoUploadDto>> GetUploadsForAthleteAsync(string athleteId);
        Task<VideoUpload?> GetUploadAsync(string videoId);
        Task<VideoAnalysisResult?> GetResultAsync(string videoId);

        // This clip's metrics vs the athlete's own personal best across their other
        // Complete clips — never a fabricated "position benchmark". Returns null when
        // the video itself has no result yet.
        Task<VideoComparisonDto?> GetComparisonAsync(string videoId);

        // Marks the upload Cancelled immediately. The AI pipeline has no hard-kill hook,
        // so any already-running analysis keeps computing in the background, but
        // HandleCallbackAsync discards its result instead of overwriting the Cancelled state.
        Task<bool> CancelAsync(string videoId);

        // Deletes the DB rows and the video/keyframe files on disk.
        Task<bool> DeleteAsync(string videoId);
    }
}
