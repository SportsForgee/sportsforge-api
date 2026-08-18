namespace ForgeInsole.Data.Entities
{
    public enum InsoleStatus { Active, Inactive }

    public enum InsoleSide { L, R }

    // Where this insole's readings come from. Status (Active/Inactive) deliberately does NOT
    // carry this meaning — it only says whether the insole is in service at all. Without a
    // separate axis, a real device set to Active would have InsoleTelemetryHostedService
    // generating fake readings *while* the device pushed real ones into the same table.
    // InsoleTelemetryHostedService skips anything that is not Simulated.
    public enum InsoleSource { Simulated, Device }

    // InsoleId is the external-facing identifier Khoi Tech picks from GET /insoles —
    // it doubles as the primary key so there's no internal/external id split to keep in sync.
    public class Insole
    {
        public string InsoleId { get; set; } = "";
        public string Label { get; set; } = "";
        public InsoleStatus Status { get; set; } = InsoleStatus.Active;
        public string Firmware { get; set; } = "";
        public InsoleSide Side { get; set; }
        public InsoleSource Source { get; set; } = InsoleSource.Simulated;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
