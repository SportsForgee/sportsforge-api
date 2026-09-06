namespace Api.Models.Dtos
{
    public class VideoUploadDto
    {
        public string   Id                { get; set; } = "";
        public string   FileName          { get; set; } = "";
        public string   Status            { get; set; } = "";
        public double?  DurationSeconds   { get; set; }
        public DateTime CreatedAt         { get; set; }
        public string   VideoUrl          { get; set; } = "";

        // Present once analysis is Complete
        public double?  TopSpeedKmh       { get; set; }
        public double?  GaitBalanceScore  { get; set; }
        public double?  SymmetryScore     { get; set; }
        public int?     AnomalyCount      { get; set; }
    }

    // ── Payload the AI service POSTs back to the callback endpoint ──────────────
    // Mirrors hardware-integration/schemas/video-analysis-result.json exactly.
    public class VideoAnalysisCallbackRequest
    {
        public string  VideoId          { get; set; } = "";
        public string  AthleteId        { get; set; } = "";
        public string  Status           { get; set; } = ""; // Complete | Failed
        public double? DurationSeconds  { get; set; }
        public double? Fps              { get; set; }
        public string? FailureReason    { get; set; }

        // Full clip with the tracked skeleton drawn on every sampled frame.
        public string? AnnotatedVideoUrl { get; set; }

        public PoseMetricsDto?  PoseMetrics  { get; set; }
        public SpeedMetricsDto? SpeedMetrics { get; set; }
        public GaitBalanceDto?  GaitBalance  { get; set; }
        public List<VideoAnomalyDto> Anomalies { get; set; } = new();
        public List<VideoKeyframeDto> Keyframes { get; set; } = new();
        public List<VideoDrillRecommendationDto> DrillRecommendations { get; set; } = new();
    }

    public class VideoDrillRecommendationDto
    {
        public string AnomalyType  { get; set; } = "";
        public string Title        { get; set; } = "";
        public string Description  { get; set; } = "";
        public string Category     { get; set; } = ""; // strength | endurance | skill | speed | recovery | general
    }

    // ── Compare tab: this clip vs the athlete's own history — never a fabricated
    // "position benchmark" (no such dataset exists here; see VideoAnalysisService). ──
    public class VideoComparisonDto
    {
        public int ClipsComparedCount { get; set; }
        public MetricComparisonDto? GaitBalance  { get; set; }
        public MetricComparisonDto? Symmetry     { get; set; }
        public MetricComparisonDto? TopSpeedKmh  { get; set; }
    }

    public class MetricComparisonDto
    {
        public double Current           { get; set; }
        public double PersonalBest      { get; set; }
        public double Delta             { get; set; } // Current - PersonalBest
        public bool   IsNewPersonalBest { get; set; }
    }

    public class PoseMetricsDto
    {
        public List<JointAngleWindowDto> JointAngleWindows { get; set; } = new();
        public double? SymmetryScore          { get; set; }
        public double? PostureStabilityScore  { get; set; }

        // Trust/accuracy transparency: how much of the clip the pose model actually
        // had a confident lock on a person.
        public double? DetectionConfidence       { get; set; }
        public int?    FramesSampled             { get; set; }
        public int?    FramesWithPersonDetected   { get; set; }
    }

    public class JointAngleWindowDto
    {
        public double  StartSeconds     { get; set; }
        public double  EndSeconds       { get; set; }
        public double? AvgKneeAngleDeg  { get; set; }
        public double? AvgHipAngleDeg   { get; set; }
        public double? AvgAnkleAngleDeg { get; set; }
    }

    public class SpeedMetricsDto
    {
        public double?           TopSpeedKmh          { get; set; }
        public double?           AvgSpeedKmh           { get; set; }
        public List<double>      AccelerationPeaksMs2  { get; set; } = new();
        public double?           StrideFrequencyHz     { get; set; }
        public CalibrationDto?   Calibration           { get; set; }
    }

    public class CalibrationDto
    {
        public string Method     { get; set; } = "uncalibrated"; // pitch-markings | manual-distance | uncalibrated
        public double Confidence { get; set; }
    }

    public class GaitBalanceDto
    {
        public double Score { get; set; }
    }

    public class VideoAnomalyDto
    {
        public string Type             { get; set; } = "";
        public string Severity         { get; set; } = "Info"; // Info | Warning | Critical
        public double TimestampSeconds { get; set; }
        public string Description      { get; set; } = "";
    }

    public class VideoKeyframeDto
    {
        public double TimestampSeconds { get; set; }
        public string Label            { get; set; } = "";
        public string ThumbnailUrl     { get; set; } = "";
    }

    // ── Request .NET sends TO the AI service to kick off analysis ───────────────
    public class RequestVideoAnalysisPayload
    {
        public string VideoId     { get; set; } = "";
        public string AthleteId   { get; set; } = "";
        public string VideoPath   { get; set; } = "";
        public string CallbackUrl { get; set; } = "";
    }
}
