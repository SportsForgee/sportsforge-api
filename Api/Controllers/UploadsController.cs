using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Api.Models;

namespace Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/uploads")]
    public class UploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly UploadOptions _opts;

        public UploadsController(IWebHostEnvironment env, IOptions<UploadOptions> opts)
        {
            _env = env;
            _opts = opts.Value;
        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file provided");

            if (file.Length > _opts.MaxFileBytes) return BadRequest($"File too large. Max is {_opts.MaxFileBytes / (1024*1024)} MB");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_opts.AllowedExtensions.Contains(ext)) return BadRequest("Unsupported file type");

            var uploads = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "uploads");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(uploads, fileName);

            await using (var stream = System.IO.File.Create(path))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
            return Ok(new { url });
        }

        [HttpGet]
        public IActionResult List()
        {
            var uploads = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "uploads");
            if (!Directory.Exists(uploads)) return Ok(Array.Empty<object>());
            var files = Directory.GetFiles(uploads)
                .Select(f => new {
                    name = Path.GetFileName(f),
                    url = $"{Request.Scheme}://{Request.Host}/uploads/{Path.GetFileName(f)}",
                    size = new FileInfo(f).Length,
                    created = new FileInfo(f).CreationTimeUtc
                });
            return Ok(files);
        }

        [HttpGet("config")]
        public IActionResult Config()
        {
            return Ok(new {
                maxFileBytes = _opts.MaxFileBytes,
                allowedExtensions = _opts.AllowedExtensions,
                maxAgeDays = _opts.MaxAgeDays
            });
        }

        [HttpDelete("{filename}")]
        public IActionResult Delete(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return BadRequest();
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return BadRequest();

            var uploads = Path.Combine(_env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "uploads");
            var path = Path.Combine(uploads, filename);
            if (!System.IO.File.Exists(path)) return NotFound();
            System.IO.File.Delete(path);
            return NoContent();
        }
    }
}
