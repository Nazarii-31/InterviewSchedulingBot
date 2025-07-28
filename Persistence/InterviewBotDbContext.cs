using Microsoft.EntityFrameworkCore;
using InterviewBot.Domain.Entities;

namespace InterviewBot.Persistence
{
    public class InterviewBotDbContext : DbContext
    {
        public InterviewBotDbContext(DbContextOptions<InterviewBotDbContext> options) : base(options) { }
        
        public DbSet<Interview> Interviews { get; set; } = null!;
        public DbSet<Participant> Participants { get; set; } = null!;
        public DbSet<InterviewParticipant> InterviewParticipants { get; set; } = null!;
        public DbSet<AvailabilityRecord> AvailabilityRecords { get; set; } = null!;
        public DbSet<TimeSlot> TimeSlots { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Interview entity configuration
            modelBuilder.Entity<Interview>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.Duration).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.Status);
            });
            
            // Participant entity configuration
            modelBuilder.Entity<Participant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.GraphUserId).HasMaxLength(100);
                
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.GraphUserId);
            });
            
            // InterviewParticipant entity configuration (many-to-many)
            modelBuilder.Entity<InterviewParticipant>(entity =>
            {
                entity.HasKey(e => new { e.InterviewId, e.ParticipantId });
                
                entity.HasOne(e => e.Interview)
                    .WithMany(i => i.InterviewParticipants)
                    .HasForeignKey(e => e.InterviewId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.Participant)
                    .WithMany(p => p.InterviewParticipants)
                    .HasForeignKey(e => e.ParticipantId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.Property(e => e.Role).IsRequired();
                entity.Property(e => e.Status).IsRequired();
            });
            
            // AvailabilityRecord entity configuration
            modelBuilder.Entity<AvailabilityRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.LastUpdated).IsRequired();
                
                entity.HasOne(e => e.Participant)
                    .WithMany(p => p.AvailabilityRecords)
                    .HasForeignKey(e => e.ParticipantId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasIndex(e => new { e.ParticipantId, e.Date }).IsUnique();
            });
            
            // TimeSlot entity configuration
            modelBuilder.Entity<TimeSlot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.EndTime).IsRequired();
                
                entity.HasOne(e => e.AvailabilityRecord)
                    .WithMany(ar => ar.TimeSlots)
                    .HasForeignKey(e => e.AvailabilityRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.EndTime);
            });
            
            // Configure value objects and enums
            modelBuilder.Entity<Interview>()
                .Property(e => e.Status)
                .HasConversion<string>();
                
            modelBuilder.Entity<InterviewParticipant>()
                .Property(e => e.Role)
                .HasConversion<string>();
                
            modelBuilder.Entity<InterviewParticipant>()
                .Property(e => e.Status)
                .HasConversion<string>();
        }
    }
}