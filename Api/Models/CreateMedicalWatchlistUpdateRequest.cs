namespace Api.Models
{
    public class CreateMedicalWatchlistUpdateRequest
    {
        public string PlayerName { get; set; } = string.Empty;

        public string Issue { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string Priority { get; set; } = string.Empty;
    }
}