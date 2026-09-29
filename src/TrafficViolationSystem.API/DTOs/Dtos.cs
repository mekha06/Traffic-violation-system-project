using System.ComponentModel.DataAnnotations;
using TrafficViolationSystem.API.Models;

namespace TrafficViolationSystem.API.DTOs;

// ──────────────────────────────────────────────
// Vehicle DTOs
// ──────────────────────────────────────────────

public class CreateVehicleDto
{
    [Required]
    [StringLength(20)]
    public string PlateNumber { get; set; } = string.Empty;

    [StringLength(100)]
    public string? OwnerName { get; set; }

    [StringLength(50)]
    public string? VehicleType { get; set; }
}

public class VehicleResponseDto
{
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string? VehicleType { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ──────────────────────────────────────────────
// Junction DTOs
// ──────────────────────────────────────────────

public class CreateJunctionDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Location { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class JunctionResponseDto
{
    public int JunctionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ──────────────────────────────────────────────
// Violation Case DTOs
// ──────────────────────────────────────────────

public class CreateViolationCaseDto
{
    [Required]
    [StringLength(100)]
    public string ViolationType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Junction where the violation occurred.
    /// </summary>
    [Required]
    public int JunctionId { get; set; }

    /// <summary>
    /// When the violation occurred. Defaults to now if not supplied.
    /// </summary>
    public DateTime? ViolationDate { get; set; }
}

public class ViolationCaseResponseDto
{
    public int CaseId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public string? RecognizedPlate { get; set; }
    public double? OcrConfidence { get; set; }
    public string? CorrectedPlate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? AiSummary { get; set; }
    public DateTime ViolationDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Related entities
    public VehicleResponseDto Vehicle { get; set; } = null!;
    public JunctionResponseDto Junction { get; set; } = null!;
}

public class ReviewCaseDto
{
    /// <summary>
    /// New status: "Approved" or "Rejected".
    /// </summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Name of the reviewing officer.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>
    /// Manually corrected plate number (if the OCR result was wrong).
    /// </summary>
    [StringLength(20)]
    public string? CorrectedPlate { get; set; }
}

// ──────────────────────────────────────────────
// OCR Service DTOs
// ──────────────────────────────────────────────

public class OcrResultDto
{
    public string PlateNumber { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

// ──────────────────────────────────────────────
// Gemini AI DTOs
// ──────────────────────────────────────────────

public class GenerateSummaryRequestDto
{
    public string PlateNumber { get; set; } = string.Empty;
    public string JunctionName { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public DateTime ViolationDate { get; set; }
    public string? Description { get; set; }
}
