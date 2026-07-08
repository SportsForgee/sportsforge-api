namespace Api.Models
{
    // One row per athlete — doctor-managed match clearance and clinical notes.
    // Distinct from MedicalWatchlistItem (in-memory club demo data); this is the
    // real, EF-backed per-athlete record the doctor dashboard reads and writes.
    public class AthleteMedicalRecord
    {
        public int      Id               { get; set; }
        public string   AthleteId        { get; set; } = "";
        public bool     ClearanceGranted { get; set; } = true;
        public string?  Notes            { get; set; }
        public string?  UpdatedByDoctorId { get; set; }
        public DateTime UpdatedAt        { get; set; } = DateTime.UtcNow;

        public AppUser? Athlete { get; set; }
    }
}
