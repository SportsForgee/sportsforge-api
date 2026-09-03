namespace Api.Models.Dtos
{
    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }
        public Dictionary<string, int> UsersByRole { get; set; } = new();
        public int SuspendedUsers { get; set; }
        public int NewUsersLast7Days { get; set; }
        public int ActiveSessionsLast24Hours { get; set; }
        public int TotalOrganisations { get; set; }
        public int TotalVideos { get; set; }
        public int VideosAnalysedToday { get; set; }
        public int TotalDevices { get; set; }
        public int AthletesVisibleToScouts { get; set; }
    }

    public class AdminUserDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string Role { get; set; } = "";
        public string? Organisation { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSuspended { get; set; }
        public DateTimeOffset? SuspendedUntil { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class SuspendUserRequest
    {
        // Null means suspend indefinitely.
        public int? Days { get; set; }
        public string? Reason { get; set; }
    }

    public class AuditLogDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Details { get; set; }
        public bool Success { get; set; }
        public string? UserEmail { get; set; }
        public string? UserRole { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AiMonitorDto
    {
        public bool ServiceReachable { get; set; }
        public string? BaseUrl { get; set; }
        public int? LatencyMs { get; set; }
        public bool OpenCvAvailable { get; set; }
        public bool MediapipeAvailable { get; set; }
        public bool ModelReady { get; set; }
        public string? Error { get; set; }

        public int JobsQueued { get; set; }
        public int JobsProcessing { get; set; }
        public int JobsComplete { get; set; }
        public int JobsFailed { get; set; }
        public int AnalysesLast24Hours { get; set; }
    }

    public class PlatformSettingDto
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateSettingRequest
    {
        public string Value { get; set; } = "";
    }
}
