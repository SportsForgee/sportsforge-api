using System.Net.Http.Json;
using Api.Models.Dtos;

namespace Api.Services
{
    public interface IVideoAnalysisAiClient
    {
        // Fire-and-forget kickoff — ai-service returns 202 immediately and posts the
        // result back to callbackUrl when done. Returns false if the AI service itself
        // couldn't be reached (network/startup failure, not an analysis failure).
        Task<bool> RequestAnalysisAsync(RequestVideoAnalysisPayload payload, CancellationToken ct = default);
    }

    public class VideoAnalysisAiClient : IVideoAnalysisAiClient
    {
        private readonly HttpClient _http;

        public VideoAnalysisAiClient(HttpClient http) => _http = http;

        public async Task<bool> RequestAnalysisAsync(RequestVideoAnalysisPayload payload, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/video/analyze", payload, ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
