using Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Message>       Messages       { get; set; }
        public DbSet<Channel>       Channels       { get; set; }
        public DbSet<ChannelMember> ChannelMembers { get; set; }

        // Training management
        public DbSet<Drill>               Drills               { get; set; }
        public DbSet<TrainingSession>     TrainingSessions     { get; set; }
        public DbSet<SessionDrill>        SessionDrills        { get; set; }
        public DbSet<SessionParticipant>  SessionParticipants  { get; set; }
        public DbSet<DrillCompletion>     DrillCompletions     { get; set; }

        // Hardware telemetry (Forge Insole + Khoi wearable)
        public DbSet<Device>          Devices          { get; set; }
        public DbSet<InsoleReading>   InsoleReadings   { get; set; }
        public DbSet<WearableReading> WearableReadings { get; set; }
        public DbSet<SyncSession>     SyncSessions     { get; set; }

        // Doctor-managed athlete medical records
        public DbSet<AthleteMedicalRecord> AthleteMedicalRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Message>(e =>
            {
                e.HasIndex(m => m.SenderId);
                e.HasIndex(m => m.ReceiverId);
                e.HasIndex(m => m.ChannelId);
                e.HasIndex(m => m.SentAt);
                e.HasOne(m => m.Sender)
                 .WithMany()
                 .HasForeignKey(m => m.SenderId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ChannelMember>(e =>
            {
                e.HasIndex(cm => new { cm.ChannelId, cm.UserId }).IsUnique();
                e.HasOne(cm => cm.Channel)
                 .WithMany(c => c.Members)
                 .HasForeignKey(cm => cm.ChannelId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Channel>(e =>
            {
                e.HasMany(c => c.Messages)
                 .WithOne()
                 .HasForeignKey(m => m.ChannelId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Training
            builder.Entity<Drill>(e =>
            {
                e.HasOne(d => d.Coach)
                 .WithMany()
                 .HasForeignKey(d => d.CoachId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(d => d.CoachId);
            });

            builder.Entity<TrainingSession>(e =>
            {
                e.HasOne(s => s.Coach)
                 .WithMany()
                 .HasForeignKey(s => s.CoachId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(s => s.CoachId);
                e.HasIndex(s => s.ScheduledAt);
            });

            builder.Entity<SessionDrill>(e =>
            {
                e.HasOne(sd => sd.Session)
                 .WithMany(s => s.Drills)
                 .HasForeignKey(sd => sd.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(sd => sd.Drill)
                 .WithMany()
                 .HasForeignKey(sd => sd.DrillId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SessionParticipant>(e =>
            {
                e.HasOne(sp => sp.Session)
                 .WithMany(s => s.Participants)
                 .HasForeignKey(sp => sp.SessionId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(sp => sp.Athlete)
                 .WithMany()
                 .HasForeignKey(sp => sp.AthleteId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(sp => new { sp.SessionId, sp.AthleteId }).IsUnique();
            });

            builder.Entity<DrillCompletion>(e =>
            {
                e.HasOne(dc => dc.SessionDrill)
                 .WithMany(sd => sd.Completions)
                 .HasForeignKey(dc => dc.SessionDrillId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(dc => dc.Athlete)
                 .WithMany()
                 .HasForeignKey(dc => dc.AthleteId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(dc => new { dc.SessionDrillId, dc.AthleteId }).IsUnique();
            });

            // Hardware telemetry
            builder.Entity<Device>(e =>
            {
                e.HasOne(d => d.Athlete)
                 .WithMany()
                 .HasForeignKey(d => d.AthleteId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(d => d.AthleteId);
                e.HasIndex(d => d.SerialNumber).IsUnique();
            });

            builder.Entity<InsoleReading>(e =>
            {
                e.HasOne(r => r.Device)
                 .WithMany()
                 .HasForeignKey(r => r.DeviceId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Athlete)
                 .WithMany()
                 .HasForeignKey(r => r.AthleteId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(r => new { r.AthleteId, r.Timestamp });
            });

            builder.Entity<WearableReading>(e =>
            {
                e.HasOne(r => r.Device)
                 .WithMany()
                 .HasForeignKey(r => r.DeviceId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Athlete)
                 .WithMany()
                 .HasForeignKey(r => r.AthleteId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(r => new { r.AthleteId, r.Timestamp });
            });

            builder.Entity<SyncSession>(e =>
            {
                e.HasOne(s => s.Device)
                 .WithMany()
                 .HasForeignKey(s => s.DeviceId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => s.DeviceId);
            });

            builder.Entity<AthleteMedicalRecord>(e =>
            {
                e.HasOne(r => r.Athlete)
                 .WithMany()
                 .HasForeignKey(r => r.AthleteId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.AthleteId).IsUnique();
            });
        }
    }
}
