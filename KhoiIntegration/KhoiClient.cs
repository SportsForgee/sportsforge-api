using System.Net.Http.Json;

namespace KhoiIntegration
{
    // Talks to the Khoi Cloud API. BaseAddress and the Authorization header are
    // configured by the caller (backend/Api's Program.cs, via AddHttpClient) from
    // Khoi:BaseUrl / Khoi:ApiKey so this project has no dependency on ASP.NET
    // configuration types.
    public class KhoiClient : IKhoiClient
    {
        private readonly HttpClient _http;

        public KhoiClient(HttpClient http) => _http = http;

        public async Task<KhoiCloudResponse?> GetLatestReadingsAsync(string externalAthleteId, CancellationToken ct = default)
        {
            var response = await _http.GetAsync($"/v1/athletes/{externalAthleteId}/readings/latest", ct);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<KhoiCloudResponse>(cancellationToken: ct);
        }
    }
}
