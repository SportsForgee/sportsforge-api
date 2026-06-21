namespace Api.Models
{
    public class UploadOptions
    {
        public long MaxFileBytes { get; set; } = 200 * 1024 * 1024; // 200 MB
        public string[] AllowedExtensions { get; set; } = new[] { ".mp4", ".mov", ".webm", ".ogg", ".mkv" };
        public int MaxAgeDays { get; set; } = 30;
    }
}
