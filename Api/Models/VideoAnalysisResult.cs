namespace Api.Models
{
    public class VideoAnalysisResult
    {
        public int      Id              { get; set; }
        public string   VideoUploadId   { get; set; } = "";

        // Full result payload, matching hardware-integration/schemas/video-analysis-result.json
        public string   ResultJson      { get; set; } = "";

        // Denormalized for querying/summary joins without parsing ResultJson every time
        public double?  TopSpeedKmh        { get; set; }
        public double?  GaitBalanceScore   { get; set; }
        public double?  SymmetryScore      { get; set; }
        public int      AnomalyCount       { get; set; }
        public string?  CalibrationMethod  { get; set; } // pitch-markings | manual-distance | uncalibrated

        public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;

        public VideoUpload? VideoUpload { get; set; }
    }
}
