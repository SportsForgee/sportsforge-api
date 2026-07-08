namespace Api.Models
{
    public class VideoUpload
    {
        public string   Id               { get; set; } = Guid.NewGuid().ToString();
        public string   AthleteId        { get; set; } = "";
        public string   UploadedByUserId { get; set; } = "";
        public string   FileName         { get; set; } = "";
        public string   StoragePath      { get; set; } = "";
        public long     SizeBytes        { get; set; }
        public double?  DurationSeconds  { get; set; }
        public string   Status           { get; set; } = "Queued"; // Queued | Processing | Complete | Failed
        public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;

        public AppUser? Athlete          { get; set; }
        public AppUser? UploadedBy       { get; set; }
    }
}
