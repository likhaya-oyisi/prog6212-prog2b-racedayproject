using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core Code-First context for the RaceDay database. The generated schema
/// matches the Part 1 ERD and SQL script (users, events, categories,
/// enrollments, results).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // events.organiser_id -> users.user_id (RESTRICT on delete)
        modelBuilder.Entity<Event>()
            .HasOne(e => e.Organiser)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.OrganiserId)
            .OnDelete(DeleteBehavior.Restrict);

        // categories.event_id -> events.event_id (CASCADE on delete)
        modelBuilder.Entity<Category>()
            .HasOne(c => c.Event)
            .WithMany(e => e.Categories)
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique (event, name) + alternate key for the composite enrollment FK
        modelBuilder.Entity<Category>()
            .HasIndex(c => new { c.EventId, c.Name }).IsUnique();
        modelBuilder.Entity<Category>()
            .HasAlternateKey(c => new { c.EventId, c.CategoryId });

        // enrollments.participant_id -> users.user_id (RESTRICT)
        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Participant)
            .WithMany(u => u.Enrollments)
            .HasForeignKey(e => e.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        // enrollments.event_id -> events.event_id (RESTRICT)
        // Configured without a second navigation FK conflict: EventId is also part of
        // the composite category FK below, so only that relationship owns EventId.
        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Event)
            .WithMany(ev => ev.Enrollments)
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        // Composite FK mirrors the Part 1 script: (event_id, category_id) -> categories
        modelBuilder.Entity<Enrollment>()
            .HasOne(e => e.Category)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => new { e.EventId, e.CategoryId })
            .HasPrincipalKey(c => new { c.EventId, c.CategoryId })
            .OnDelete(DeleteBehavior.Restrict);

        // uq_enrollments_participant_event: one enrollment per participant per event
        modelBuilder.Entity<Enrollment>()
            .HasIndex(e => new { e.ParticipantId, e.EventId }).IsUnique();

        // results.enrollment_id -> enrollments.enrollment_id (CASCADE), and unique
        modelBuilder.Entity<Result>()
            .HasOne(r => r.Enrollment)
            .WithOne(e => e.Result)
            .HasForeignKey<Result>(r => r.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Result>()
            .HasIndex(r => r.EnrollmentId).IsUnique();

        // CHECK constraints mirroring the Part 1 script
        modelBuilder.Entity<Event>()
            .ToTable(t => t.HasCheckConstraint("chk_events_distance_positive", "[DistanceKm] > 0"));
        modelBuilder.Entity<Result>()
            .ToTable(t => t.HasCheckConstraint("chk_results_overall_position", "[OverallPosition] > 0"));
        modelBuilder.Entity<Result>()
            .ToTable(t => t.HasCheckConstraint("chk_results_category_position", "[CategoryPosition] IS NULL OR [CategoryPosition] > 0"));
    }
}
