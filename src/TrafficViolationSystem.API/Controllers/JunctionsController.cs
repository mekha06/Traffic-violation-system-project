using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.DTOs;
using TrafficViolationSystem.API.Models;

namespace TrafficViolationSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JunctionsController : ControllerBase
{
    private readonly TrafficDbContext _context;

    public JunctionsController(TrafficDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all junctions.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<JunctionResponseDto>>> GetAll()
    {
        var junctions = await _context.Junctions
            .OrderBy(j => j.Name)
            .Select(j => MapToDto(j))
            .ToListAsync();

        return Ok(junctions);
    }

    /// <summary>
    /// Get a junction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<JunctionResponseDto>> GetById(int id)
    {
        var junction = await _context.Junctions.FindAsync(id);
        if (junction == null) return NotFound();

        return Ok(MapToDto(junction));
    }

    /// <summary>
    /// Create a new junction.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<JunctionResponseDto>> Create([FromBody] CreateJunctionDto dto)
    {
        var junction = new Junction
        {
            Name = dto.Name,
            Location = dto.Location,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        _context.Junctions.Add(junction);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = junction.JunctionId }, MapToDto(junction));
    }

    private static JunctionResponseDto MapToDto(Junction j) => new()
    {
        JunctionId = j.JunctionId,
        Name = j.Name,
        Location = j.Location,
        Latitude = j.Latitude,
        Longitude = j.Longitude,
        CreatedAt = j.CreatedAt
    };
}
