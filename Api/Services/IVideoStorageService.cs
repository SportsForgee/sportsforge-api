namespace Api.Services
{
    public interface IVideoStorageService
    {
        // Saves the uploaded stream to disk and returns the absolute path it was written to.
        Task<string> SaveAsync(string athleteId, string fileName, Stream content, CancellationToken ct = default);

        // Absolute path a keyframe/thumbnail should be written to, and the URL it's served at.
        (string AbsolutePath, string PublicUrl) GetThumbnailPath(string athleteId, string videoId, string thumbnailFileName);

        // Public URL for a previously-saved raw video, given the absolute path SaveAsync returned.
        string GetPublicUrl(string athleteId, string absolutePath);

        // Removes the raw video file and its keyframes directory. Best-effort — a locked
        // or already-missing file shouldn't block the caller from deleting the DB record.
        void DeleteVideoFiles(string athleteId, string videoId, string absoluteVideoPath);
    }

    // Local-disk implementation for the MVP. Swap for blob storage later behind this same
    // interface — nothing above this layer needs to change (same pattern as
    // IHardwareTelemetryService's simulator-first design).
    public class LocalVideoStorageService : IVideoStorageService
    {
        private readonly string _root;
        private readonly string _publicBaseUrl;

        public LocalVideoStorageService(IConfiguration config)
        {
            var configuredRoot = config["Video:StorageRoot"];
            _root = string.IsNullOrWhiteSpace(configuredRoot) ? Path.Combine(AppContext.BaseDirectory, "storage", "videos") : configuredRoot;
            var configuredPublicUrl = config["Video:PublicBaseUrl"];
            _publicBaseUrl = string.IsNullOrWhiteSpace(configuredPublicUrl) ? "/storage/videos" : configuredPublicUrl;
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(string athleteId, string fileName, Stream content, CancellationToken ct = default)
        {
            var athleteDir = Path.Combine(_root, athleteId);
            Directory.CreateDirectory(athleteDir);

            var safeName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
            var fullPath = Path.Combine(athleteDir, safeName);

            await using var fileStream = File.Create(fullPath);
            await content.CopyToAsync(fileStream, ct);

            return fullPath;
        }

        public (string AbsolutePath, string PublicUrl) GetThumbnailPath(string athleteId, string videoId, string thumbnailFileName)
        {
            var dir = Path.Combine(_root, athleteId, videoId, "keyframes");
            Directory.CreateDirectory(dir);
            var absolutePath = Path.Combine(dir, thumbnailFileName);
            var publicUrl = $"{_publicBaseUrl}/{athleteId}/{videoId}/keyframes/{thumbnailFileName}";
            return (absolutePath, publicUrl);
        }

        public string GetPublicUrl(string athleteId, string absolutePath)
        {
            var fileName = Path.GetFileName(absolutePath);
            return $"{_publicBaseUrl}/{athleteId}/{fileName}";
        }

        public void DeleteVideoFiles(string athleteId, string videoId, string absoluteVideoPath)
        {
            try { if (File.Exists(absoluteVideoPath)) File.Delete(absoluteVideoPath); } catch { /* best-effort */ }
            try
            {
                var videoDir = Path.Combine(_root, athleteId, videoId);
                if (Directory.Exists(videoDir)) Directory.Delete(videoDir, recursive: true);
            }
            catch { /* best-effort */ }
        }
    }
}
