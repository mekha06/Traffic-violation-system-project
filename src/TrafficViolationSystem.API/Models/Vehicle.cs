using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrafficViolationSystem.API.Models;

/// <summary>
/// Represents a vehicle identified in the system.
/// </summary>
public class Vehicle
{
    [Key]
    public int VehicleId { get; set; }

    /// <summary>
    /// The recognized or manually corrected number plate.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional owner name if known.
    /// </summary>
    [StringLength(100)]
    public string? OwnerName { get; set; }

    /// <summary>
    /// Type of vehicle (Car, Truck, Motorcycle, etc.)
    /// </summary>
    [StringLength(50)]
    public string? VehicleType { get; set; }

    /// <summary>
    /// Date when this vehicle record was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ViolationCase> ViolationCases { get; set; } = new List<ViolationCase>();
}
