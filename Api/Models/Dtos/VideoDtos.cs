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
        public BallMetricsDto?  BallMetrics  { get; set; }
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

    // ── Phase 5: clip-vs-clip comparison — any two of the athlete's own analyzed
    // clips, picked explicitly (e.g. "before" vs "after" technique work), as opposed
    // to the always-vs-personal-best comparison above. Metrics are only populated
    // when BOTH clips have a real (non-null, calibrated where relevant) value — never
    // a fabricated number on one side. ──────────────────────────────────────────────
    public class ClipPairComparisonDto
    {
        public ClipSummaryDto ClipA { get; set; } = new();
        public ClipSummaryDto ClipB { get; set; } = new();
        public ClipPairMetricDto? TopSpeedKmh      { get; set; }
        public ClipPairMetricDto? GaitBalance      { get; set; }
        public ClipPairMetricDto? Symmetry         { get; set; }
        public ClipPairMetricDto? PostureStability { get; set; }
        public ClipPairMetricDto? TopShotSpeedKmh  { get; set; }
        public int AnomalyCountA { get; set; }
        public int AnomalyCountB { get; set; }
    }

    public class ClipSummaryDto
    {
        public string   Id        { get; set; } = "";
        public string   FileName  { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string   VideoUrl  { get; set; } = "";
    }

    public class ClipPairMetricDto
    {
        public double ValueA { get; set; }
        public double ValueB { get; set; }
        public double Delta  { get; set; } // ValueB - ValueA
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

    // ── Phase 3: football/ball detection + trajectory tracking (YOLOv8) ─────────
    public class BallTrajectoryPointDto
    {
        public double TimestampSeconds { get; set; }
        public double XNorm            { get; set; } // ball center, normalized 0-1 across frame width
        public double YNorm            { get; set; } // ball center, normalized 0-1 across frame height
        public double Confidence        { get; set; } // YOLO detection confidence for this frame, 0-1
    }

    public class BallMetricsDto
    {
        public bool    Detected               { get; set; }
        public int?     FramesSampled           { get; set; }
        public int?     FramesWithBallDetected  { get; set; }
        public double?  DetectionConfidence     { get; set; }
        public List<BallTrajectoryPointDto> Trajectory { get; set; } = new();
        public double?  TopSpeedKmh             { get; set; }
        public double?  AvgSpeedKmh             { get; set; }
        // Phase 4: discrete shot/strike events found in the ball's trajectory.
        public List<ShotSpeedEventDto> Shots    { get; set; } = new();
        public int?     ShotCount               { get; set; }
        public double?  TopShotSpeedKmh         { get; set; }
        public CalibrationDto? Calibration      { get; set; }
    }

    // ── Phase 4: shot/ball-speed estimation — frame-to-frame ball speed spikes far
    // above the clip's own typical ball movement (kicks/strikes), built on the Phase 3
    // trajectory above. ────────────────────────────────────────────────────────────
    public class ShotSpeedEventDto
    {
        public double  TimestampSeconds { get; set; }
        public double? SpeedKmh         { get; set; } // null unless the clip is calibrated — same rule as everywhere else
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
