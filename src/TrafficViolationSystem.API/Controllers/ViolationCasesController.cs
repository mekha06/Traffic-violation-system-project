using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficViolationSystem.API.Data;
using TrafficViolationSystem.API.DTOs;
using TrafficViolationSystem.API.Models;
using TrafficViolationSystem.API.Services;

namespace TrafficViolationSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ViolationCasesController : ControllerBase
{
    private readonly TrafficDbContext _context;
    private readonly IOcrService _ocrService;
    private readonly IGeminiService _geminiService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ViolationCasesController> _logger;

    public ViolationCasesController(
        TrafficDbContext context,
        IOcrService ocrService,
        IGeminiService geminiService,
        IWebHostEnvironment env,
        ILogger<ViolationCasesController> logger)
    {
        _context = context;
        _ocrService = ocrService;
        _geminiService = geminiService;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Get all violation cases with optional status filter.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ViolationCaseResponseDto>>> GetAll([FromQuery] string? status)
    {
        var query = _context.ViolationCases
            .Include(vc => vc.Vehicle)
            .Include(vc => vc.Junction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(vc => vc.Status == parsedStatus);
        }

        var cases = await query
            .OrderByDescending(vc => vc.CreatedAt)
            .Select(vc => MapToDto(vc))
            .ToListAsync();

        return Ok(cases);
    }

    /// <summary>
    /// Get a specific violation case by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ViolationCaseResponseDto>> GetById(int id)
    {
        var violationCase = await _context.ViolationCases
            .Include(vc => vc.Vehicle)
            .Include(vc => vc.Junction)
            .FirstOrDefaultAsync(vc => vc.CaseId == id);

        if (violationCase == null) return NotFound();

        return Ok(MapToDto(violationCase));
    }

    /// <summary>
    /// Create a new violation case by uploading an image.
    /// Flow: Upload image → OCR plate → Create/find vehicle → Save case → Generate AI summary.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ViolationCaseResponseDto>> Create(
        [FromForm] CreateViolationCaseDto dto,
        IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest(new { message = "An image file is required." });

        // Validate junction exists
        var junction = await _context.Junctions.FindAsync(dto.JunctionId);
        if (junction == null)
            return BadRequest(new { message = $"Junction with ID {dto.JunctionId} not found." });

        // 1. Save the image to local filesystem
        var imagePath = await SaveImageAsync(image);

        // 2. Call OCR service to recognize the plate
        OcrResultDto? ocrResult = null;
        using (var imageStream = image.OpenReadStream())
        {
            ocrResult = await _ocrService.RecognizePlateAsync(imageStream, image.FileName);
        }

        var recognizedPlate = ocrResult?.PlateNumber?.ToUpper() ?? "UNKNOWN";
        var ocrConfidence = ocrResult?.Confidence ?? 0.0;

        _logger.LogInformation("OCR result: Plate={Plate}, Confidence={Confidence}", recognizedPlate, ocrConfidence);

        // 3. Find or create the vehicle
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.PlateNumber == recognizedPlate);

        if (vehicle == null)
        {
            vehicle = new Vehicle { PlateNumber = recognizedPlate };
            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();
        }

        // 4. Create the violation case
        var violationCase = new ViolationCase
        {
            ViolationType = dto.ViolationType,
            Description = dto.Description,
            ImagePath = imagePath,
            RecognizedPlate = recognizedPlate,
            OcrConfidence = ocrConfidence,
            ViolationDate = dto.ViolationDate ?? DateTime.UtcNow,
            VehicleId = vehicle.VehicleId,
            JunctionId = junction.JunctionId,
            Status = CaseStatus.Pending
        };

        _context.ViolationCases.Add(violationCase);
        await _context.SaveChangesAsync();

        // 5. Generate AI summary (non-blocking — we save even if this fails)
        var aiSummary = await _geminiService.GenerateSummaryAsync(
            recognizedPlate,
            junction.Name,
            dto.ViolationType,
            violationCase.ViolationDate,
            dto.Description);

        if (!string.IsNullOrWhiteSpace(aiSummary))
        {
            violationCase.AiSummary = aiSummary;
            await _context.SaveChangesAsync();
        }

        // Reload with navigation properties
        await _context.Entry(violationCase).Reference(vc => vc.Vehicle).LoadAsync();
        await _context.Entry(violationCase).Reference(vc => vc.Junction).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = violationCase.CaseId }, MapToDto(violationCase));
    }

    /// <summary>
    /// Review a violation case — approve or reject, optionally correct the plate number.
    /// </summary>
    [HttpPut("{id:int}/review")]
    public async Task<ActionResult<ViolationCaseResponseDto>> ReviewCase(int id, [FromBody] ReviewCaseDto dto)
    {
        var violationCase = await _context.ViolationCases
            .Include(vc => vc.Vehicle)
            .Include(vc => vc.Junction)
            .FirstOrDefaultAsync(vc => vc.CaseId == id);

        if (violationCase == null) return NotFound();

        if (violationCase.Status != CaseStatus.Pending)
            return BadRequest(new { message = "This case has already been reviewed." });

        // Parse and set new status
        if (!Enum.TryParse<CaseStatus>(dto.Status, true, out var newStatus) ||
            newStatus == CaseStatus.Pending)
        {
            return BadRequest(new { message = "Status must be 'Approved' or 'Rejected'." });
        }

        violationCase.Status = newStatus;
        violationCase.ReviewerName = dto.ReviewerName;
        violationCase.ReviewedAt = DateTime.UtcNow;

        // If the officer corrected the plate, update the case and potentially the vehicle
        if (!string.IsNullOrWhiteSpace(dto.CorrectedPlate))
        {
            violationCase.CorrectedPlate = dto.CorrectedPlate.ToUpper();

            // If plate was corrected, find or create a vehicle with the corrected plate
            var correctedVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.PlateNumber == dto.CorrectedPlate.ToUpper());

            if (correctedVehicle == null)
            {
                correctedVehicle = new Vehicle { PlateNumber = dto.CorrectedPlate.ToUpper() };
                _context.Vehicles.Add(correctedVehicle);
                await _context.SaveChangesAsync();
            }

            violationCase.VehicleId = correctedVehicle.VehicleId;
        }

        await _context.SaveChangesAsync();

        // Reload navigation properties
        await _context.Entry(violationCase).Reference(vc => vc.Vehicle).LoadAsync();
        await _context.Entry(violationCase).Reference(vc => vc.Junction).LoadAsync();

        return Ok(MapToDto(violationCase));
    }

    /// <summary>
    /// Regenerate the AI summary for a case.
    /// </summary>
    [HttpPost("{id:int}/regenerate-summary")]
    public async Task<ActionResult<ViolationCaseResponseDto>> RegenerateSummary(int id)
    {
        var violationCase = await _context.ViolationCases
            .Include(vc => vc.Vehicle)
            .Include(vc => vc.Junction)
            .FirstOrDefaultAsync(vc => vc.CaseId == id);

        if (violationCase == null) return NotFound();

        var plateToUse = violationCase.CorrectedPlate ?? violationCase.RecognizedPlate ?? "UNKNOWN";

        var summary = await _geminiService.GenerateSummaryAsync(
            plateToUse,
            violationCase.Junction.Name,
            violationCase.ViolationType,
            violationCase.ViolationDate,
            violationCase.Description);

        if (string.IsNullOrWhiteSpace(summary))
            return StatusCode(503, new { message = "AI service is unavailable. Please try again later." });

        violationCase.AiSummary = summary;
        await _context.SaveChangesAsync();

        return Ok(MapToDto(violationCase));
    }

    /// <summary>
    /// Serves the uploaded violation image.
    /// </summary>
    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var violationCase = await _context.ViolationCases.FindAsync(id);
        if (violationCase == null || string.IsNullOrEmpty(violationCase.ImagePath))
            return NotFound();

        var fullPath = Path.Combine(_env.ContentRootPath, violationCase.ImagePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { message = "Image file not found on disk." });

        var contentType = "image/jpeg";
        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        if (ext == ".png") contentType = "image/png";
        else if (ext == ".webp") contentType = "image/webp";

        return PhysicalFile(fullPath, contentType);
    }

    // ──────────────────────────────────────────────
    // Private Helpers
    // ──────────────────────────────────────────────

    private async Task<string> SaveImageAsync(IFormFile image)
    {
        var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads", "ViolationImages");
        Directory.CreateDirectory(uploadsDir);

        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
        var filePath = Path.Combine(uploadsDir, uniqueFileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await image.CopyToAsync(stream);

        // Return relative path for storage
        return Path.Combine("Uploads", "ViolationImages", uniqueFileName);
    }

    private static ViolationCaseResponseDto MapToDto(ViolationCase vc) => new()
    {
        CaseId = vc.CaseId,
        ViolationType = vc.ViolationType,
        Description = vc.Description,
        ImagePath = vc.ImagePath,
        RecognizedPlate = vc.RecognizedPlate,
        OcrConfidence = vc.OcrConfidence,
        CorrectedPlate = vc.CorrectedPlate,
        Status = vc.Status.ToString(),
        ReviewerName = vc.ReviewerName,
        ReviewedAt = vc.ReviewedAt,
        AiSummary = vc.AiSummary,
        ViolationDate = vc.ViolationDate,
        CreatedAt = vc.CreatedAt,
        Vehicle = new VehicleResponseDto
        {
            VehicleId = vc.Vehicle.VehicleId,
            PlateNumber = vc.Vehicle.PlateNumber,
            OwnerName = vc.Vehicle.OwnerName,
            VehicleType = vc.Vehicle.VehicleType,
            CreatedAt = vc.Vehicle.CreatedAt
        },
        Junction = new JunctionResponseDto
        {
            JunctionId = vc.Junction.JunctionId,
            Name = vc.Junction.Name,
            Location = vc.Junction.Location,
            Latitude = vc.Junction.Latitude,
            Longitude = vc.Junction.Longitude,
            CreatedAt = vc.Junction.CreatedAt
        }
    };
}
