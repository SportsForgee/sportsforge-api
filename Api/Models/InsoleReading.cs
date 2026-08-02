namespace Api.Models
{
    public class InsoleReading
    {
        public int      Id                 { get; set; }
        public int      DeviceId           { get; set; }
        public string   AthleteId          { get; set; } = "";
        public DateTime Timestamp          { get; set; }
        public string   Foot               { get; set; } = "";   // L | R
        public string?  PressureMapJson    { get; set; }         // free-form JSON, sensor layout still being redesigned
        public double?  Cadence            { get; set; }         // steps/min
        public double?  GroundContactMs    { get; set; }
        public string?  FootStrike         { get; set; }         // Heel | Midfoot | Forefoot
        public double?  StrideAsymmetryPct { get; set; }
        public double?  ImpactForce        { get; set; }
        public double?  BalanceScore       { get; set; }         // 0-100

        public Device?  Device             { get; set; }
        public AppUser? Athlete            { get; set; }
    }
}
