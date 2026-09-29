using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.DTOs;
using TrafficViolationSystem.API.Models;

namespace TrafficViolationSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly TrafficDbContext _context;

    public VehiclesController(TrafficDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all vehicles.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleResponseDto>>> GetAll()
    {
        var vehicles = await _context.Vehicles
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => MapToDto(v))
            .ToListAsync();

        return Ok(vehicles);
    }

    /// <summary>
    /// Get a vehicle by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleResponseDto>> GetById(int id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        return Ok(MapToDto(vehicle));
    }

    /// <summary>
    /// Get a vehicle by plate number.
    /// </summary>
    [HttpGet("plate/{plateNumber}")]
    public async Task<ActionResult<VehicleResponseDto>> GetByPlate(string plateNumber)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.PlateNumber == plateNumber.ToUpper());

        if (vehicle == null) return NotFound();

        return Ok(MapToDto(vehicle));
    }

    /// <summary>
    /// Create a new vehicle.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<VehicleResponseDto>> Create([FromBody] CreateVehicleDto dto)
    {
        // Check for duplicate plate
        var existing = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.PlateNumber == dto.PlateNumber.ToUpper());

        if (existing != null)
            return Conflict(new { message = $"Vehicle with plate '{dto.PlateNumber}' already exists." });

        var vehicle = new Vehicle
        {
            PlateNumber = dto.PlateNumber.ToUpper(),
            OwnerName = dto.OwnerName,
            VehicleType = dto.VehicleType
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = vehicle.VehicleId }, MapToDto(vehicle));
    }

    private static VehicleResponseDto MapToDto(Vehicle v) => new()
    {
        VehicleId = v.VehicleId,
        PlateNumber = v.PlateNumber,
        OwnerName = v.OwnerName,
        VehicleType = v.VehicleType,
        CreatedAt = v.CreatedAt
    };
}
