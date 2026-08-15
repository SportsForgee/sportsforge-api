namespace ForgeInsole.Data.Entities
{
    public enum InsoleStatus { Active, Inactive }

    public enum InsoleSide { L, R }

    // InsoleId is the external-facing identifier Khoi Tech picks from GET /insoles —
    // it doubles as the primary key so there's no internal/external id split to keep in sync.
    public class Insole
    {
        public string InsoleId { get; set; } = "";
        public string Label { get; set; } = "";
        public InsoleStatus Status { get; set; } = InsoleStatus.Active;
        public string Firmware { get; set; } = "";
        public InsoleSide Side { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
