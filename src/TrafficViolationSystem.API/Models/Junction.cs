using System.ComponentModel.DataAnnotations;

namespace TrafficViolationSystem.API.Models;

/// <summary>
/// Represents a traffic junction / intersection where violations are monitored.
/// </summary>
public class Junction
{
    [Key]
    public int JunctionId { get; set; }

    /// <summary>
    /// Name of the junction (e.g., "Main St & 5th Ave").
    /// </summary>
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// City or area where this junction is located.
    /// </summary>
    [StringLength(100)]
    public string? Location { get; set; }

    /// <summary>
    /// Optional latitude coordinate.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Optional longitude coordinate.
    /// </summary>
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ViolationCase> ViolationCases { get; set; } = new List<ViolationCase>();
}
