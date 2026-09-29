using TrafficViolationSystem.API.DTOs;

namespace TrafficViolationSystem.API.Services;

/// <summary>
/// Communicates with the Python FastAPI OCR service to recognize number plates.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Sends an image to the Python OCR service and returns the recognized plate.
    /// </summary>
    Task<OcrResultDto?> RecognizePlateAsync(Stream imageStream, string fileName);
}
