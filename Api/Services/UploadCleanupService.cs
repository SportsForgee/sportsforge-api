using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Services
{
    public class UploadCleanupService : IHostedService, IDisposable
    {
        private readonly ILogger<UploadCleanupService> _logger;
        private readonly IWebHostEnvironment _env;
        private Timer? _timer;

        // Delete files older than this (days)
        private int MaxAgeDays = 30;

        public UploadCleanupService(ILogger<UploadCleanupService> logger, IWebHostEnvironment env, Microsoft.Extensions.Options.IOptions<Api.Models.UploadOptions> opts)
        {
            _logger = logger;
            _env = env;
            MaxAgeDays = opts.Value.MaxAgeDays;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Run cleanup every 24 hours, start after 1 minute
            _timer = new Timer(async _ => await DoCleanup(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24));
            _logger.LogInformation("UploadCleanupService started");
            return Task.CompletedTask;
        }

        private Task DoCleanup()
        {
            try
            {
                var uploads = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "uploads");
                if (!Directory.Exists(uploads)) return Task.CompletedTask;

                var threshold = DateTime.UtcNow.AddDays(-MaxAgeDays);
                var files = Directory.GetFiles(uploads);
                int removed = 0;
                foreach (var f in files)
                {
                    try
                    {
                        var info = new FileInfo(f);
                        if (info.CreationTimeUtc < threshold || info.LastWriteTimeUtc < threshold)
                        {
                            info.Delete();
                            removed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete upload {file}", f);
                    }
                }

                if (removed > 0) _logger.LogInformation("UploadCleanup removed {count} files", removed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload cleanup failed");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            _logger.LogInformation("UploadCleanupService stopped");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
