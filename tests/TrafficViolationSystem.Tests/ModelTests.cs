using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.Models;

namespace TrafficViolationSystem.Tests;

/// <summary>
/// Unit tests for the TrafficDbContext and entity models.
/// </summary>
public class ModelTests
{
    private TrafficDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TrafficDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new TrafficDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Vehicle_CanBeCreated()
    {
        using var context = CreateInMemoryContext();

        var vehicle = new Vehicle
        {
            PlateNumber = "KA01AB1234",
            OwnerName = "John Doe",
            VehicleType = "Car"
        };

        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var saved = await context.Vehicles.FirstOrDefaultAsync(v => v.PlateNumber == "KA01AB1234");
        Assert.NotNull(saved);
        Assert.Equal("John Doe", saved.OwnerName);
        Assert.Equal("Car", saved.VehicleType);
    }

    [Fact]
    public async Task Junction_CanBeCreated()
    {
        using var context = CreateInMemoryContext();

        var junction = new Junction
        {
            Name = "Test Junction",
            Location = "Test City",
            Latitude = 12.97,
            Longitude = 77.59
        };

        context.Junctions.Add(junction);
        await context.SaveChangesAsync();

        var saved = await context.Junctions.FirstOrDefaultAsync(j => j.Name == "Test Junction");
        Assert.NotNull(saved);
        Assert.Equal("Test City", saved.Location);
    }

    [Fact]
    public async Task ViolationCase_CanBeCreatedWithRelationships()
    {
        using var context = CreateInMemoryContext();

        var vehicle = new Vehicle { PlateNumber = "TEST1234" };
        var junction = new Junction { Name = "Main Junction", Location = "Downtown" };

        context.Vehicles.Add(vehicle);
        context.Junctions.Add(junction);
        await context.SaveChangesAsync();

        var violationCase = new ViolationCase
        {
            ViolationType = "Red Light",
            Description = "Ran a red light at intersection",
            RecognizedPlate = "TEST1234",
            OcrConfidence = 0.95,
            Status = CaseStatus.Pending,
            VehicleId = vehicle.VehicleId,
            JunctionId = junction.JunctionId
        };

        context.ViolationCases.Add(violationCase);
        await context.SaveChangesAsync();

        var saved = await context.ViolationCases
            .Include(vc => vc.Vehicle)
            .Include(vc => vc.Junction)
            .FirstOrDefaultAsync(vc => vc.CaseId == violationCase.CaseId);

        Assert.NotNull(saved);
        Assert.Equal("Red Light", saved.ViolationType);
        Assert.Equal(CaseStatus.Pending, saved.Status);
        Assert.Equal("TEST1234", saved.Vehicle.PlateNumber);
        Assert.Equal("Main Junction", saved.Junction.Name);
    }

    [Fact]
    public async Task ViolationCase_StatusCanBeUpdated()
    {
        using var context = CreateInMemoryContext();

        var vehicle = new Vehicle { PlateNumber = "REVIEW01" };
        var junction = new Junction { Name = "Review Junction" };

        context.Vehicles.Add(vehicle);
        context.Junctions.Add(junction);
        await context.SaveChangesAsync();

        var violationCase = new ViolationCase
        {
            ViolationType = "Speeding",
            Status = CaseStatus.Pending,
            VehicleId = vehicle.VehicleId,
            JunctionId = junction.JunctionId
        };

        context.ViolationCases.Add(violationCase);
        await context.SaveChangesAsync();

        // Simulate officer review
        violationCase.Status = CaseStatus.Approved;
        violationCase.ReviewerName = "Officer Smith";
        violationCase.ReviewedAt = DateTime.UtcNow;
        violationCase.CorrectedPlate = "REVIEW01";
        await context.SaveChangesAsync();

        var reviewed = await context.ViolationCases.FindAsync(violationCase.CaseId);
        Assert.NotNull(reviewed);
        Assert.Equal(CaseStatus.Approved, reviewed.Status);
        Assert.Equal("Officer Smith", reviewed.ReviewerName);
        Assert.NotNull(reviewed.ReviewedAt);
    }

    [Fact]
    public void CaseStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)CaseStatus.Pending);
        Assert.Equal(1, (int)CaseStatus.Approved);
        Assert.Equal(2, (int)CaseStatus.Rejected);
    }

    [Fact]
    public async Task Vehicle_HasMultipleViolationCases()
    {
        using var context = CreateInMemoryContext();

        var vehicle = new Vehicle { PlateNumber = "MULTI01" };
        var junction = new Junction { Name = "Busy Junction" };

        context.Vehicles.Add(vehicle);
        context.Junctions.Add(junction);
        await context.SaveChangesAsync();

        context.ViolationCases.AddRange(
            new ViolationCase
            {
                ViolationType = "Speeding",
                VehicleId = vehicle.VehicleId,
                JunctionId = junction.JunctionId
            },
            new ViolationCase
            {
                ViolationType = "Red Light",
                VehicleId = vehicle.VehicleId,
                JunctionId = junction.JunctionId
            }
        );
        await context.SaveChangesAsync();

        var vehicleWithCases = await context.Vehicles
            .Include(v => v.ViolationCases)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicle.VehicleId);

        Assert.NotNull(vehicleWithCases);
        Assert.Equal(2, vehicleWithCases.ViolationCases.Count);
    }
}
