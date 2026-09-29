using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrafficViolationSystem.API.Models;

/// <summary>
/// Represents a traffic violation case tied to a vehicle and junction.
/// </summary>
public class ViolationCase
{
    [Key]
    public int CaseId { get; set; }

    /// <summary>
    /// Type of violation (e.g., "Red Light", "Speeding", "Wrong Way").
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>
    /// Description or additional notes about the violation.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Path to the uploaded violation image on the local filesystem.
    /// </summary>
    [StringLength(500)]
    public string? ImagePath { get; set; }

    /// <summary>
    /// The plate number as recognized by the OCR service.
    /// </summary>
    [StringLength(20)]
    public string? RecognizedPlate { get; set; }

    /// <summary>
    /// Confidence score from the OCR service (0.0 to 1.0).
    /// </summary>
    public double? OcrConfidence { get; set; }

    /// <summary>
    /// Manually corrected plate number by the reviewing officer.
    /// </summary>
    [StringLength(20)]
    public string? CorrectedPlate { get; set; }

    /// <summary>
    /// Current review status of the case.
    /// </summary>
    public CaseStatus Status { get; set; } = CaseStatus.Pending;

    /// <summary>
    /// Name of the officer who reviewed the case.
    /// </summary>
    [StringLength(100)]
    public string? ReviewerName { get; set; }

    /// <summary>
    /// Timestamp when the case was reviewed.
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// AI-generated draft summary for officer review.
    /// </summary>
    [StringLength(2000)]
    public string? AiSummary { get; set; }

    /// <summary>
    /// When the violation occurred.
    /// </summary>
    public DateTime ViolationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this case record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Keys
    public int VehicleId { get; set; }

    [ForeignKey("VehicleId")]
    public Vehicle Vehicle { get; set; } = null!;

    public int JunctionId { get; set; }

    [ForeignKey("JunctionId")]
    public Junction Junction { get; set; } = null!;
}
