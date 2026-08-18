using ForgeInsole.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForgeInsole.Data
{
    public class ForgeInsoleDbContext : DbContext
    {
        public ForgeInsoleDbContext(DbContextOptions<ForgeInsoleDbContext> options)
            : base(options) { }

        public DbSet<Insole> Insoles { get; set; }
        public DbSet<TelemetryReading> TelemetryReadings { get; set; }
        public DbSet<SimulationSession> SimulationSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Insole>(e =>
            {
                e.HasKey(i => i.InsoleId);
                e.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
                e.Property(i => i.Side).HasConversion<string>().HasMaxLength(1);
                e.Property(i => i.Source).HasConversion<string>().HasMaxLength(16);

                e.HasData(SeedInsoles());
            });

            builder.Entity<TelemetryReading>(e =>
            {
                e.HasIndex(r => new { r.InsoleId, r.Timestamp });
                e.Property(r => r.Source).HasConversion<string>().HasMaxLength(16);
                e.HasOne<Insole>()
                 .WithMany()
                 .HasForeignKey(r => r.InsoleId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SimulationSession>(e =>
            {
                e.HasIndex(s => s.StartedAt);
            });
        }

        // Fixed CreatedAt — HasData seed values must be compile-time constants, not DateTime.UtcNow.
        private static readonly DateTime SeedCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static Insole[] SeedInsoles() => new[]
        {
            new Insole { InsoleId = "forge-insole-01", Label = "Forge Insole 01", Firmware = "1.4.0", Side = InsoleSide.L, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
            new Insole { InsoleId = "forge-insole-02", Label = "Forge Insole 02", Firmware = "1.4.0", Side = InsoleSide.R, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
            new Insole { InsoleId = "forge-insole-03", Label = "Forge Insole 03", Firmware = "1.4.0", Side = InsoleSide.L, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
            new Insole { InsoleId = "forge-insole-04", Label = "Forge Insole 04", Firmware = "1.4.0", Side = InsoleSide.R, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
            new Insole { InsoleId = "forge-insole-05", Label = "Forge Insole 05", Firmware = "1.3.2", Side = InsoleSide.L, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
            new Insole { InsoleId = "forge-insole-06", Label = "Forge Insole 06", Firmware = "1.3.2", Side = InsoleSide.R, Status = InsoleStatus.Active, CreatedAt = SeedCreatedAt },
        };
    }
}
