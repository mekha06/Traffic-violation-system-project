using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Models;

namespace TrafficViolationSystem.API.Data;

/// <summary>
/// Entity Framework Core database context for the Traffic Violation System.
/// </summary>
public class TrafficDbContext : DbContext
{
    public TrafficDbContext(DbContextOptions<TrafficDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Junction> Junctions => Set<Junction>();
    public DbSet<ViolationCase> ViolationCases => Set<ViolationCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Vehicle ──
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(v => v.PlateNumber).IsUnique();
            entity.Property(v => v.PlateNumber).IsRequired().HasMaxLength(20);
        });

        // ── Junction ──
        modelBuilder.Entity<Junction>(entity =>
        {
            entity.HasIndex(j => j.Name).IsUnique();
            entity.Property(j => j.Name).IsRequired().HasMaxLength(150);
        });

        // ── ViolationCase ──
        modelBuilder.Entity<ViolationCase>(entity =>
        {
            entity.Property(vc => vc.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasOne(vc => vc.Vehicle)
                  .WithMany(v => v.ViolationCases)
                  .HasForeignKey(vc => vc.VehicleId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(vc => vc.Junction)
                  .WithMany(j => j.ViolationCases)
                  .HasForeignKey(vc => vc.JunctionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed Data ──
        modelBuilder.Entity<Junction>().HasData(
            new Junction { JunctionId = 1, Name = "Main St & 5th Ave", Location = "Downtown", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Junction { JunctionId = 2, Name = "Highway 101 Exit 42", Location = "North District", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Junction { JunctionId = 3, Name = "Oak Road & Pine Lane", Location = "Suburban Area", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
