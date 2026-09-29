using System.Net.Http.Headers;
using System.Text.Json;
using TrafficViolationSystem.API.DTOs;

namespace TrafficViolationSystem.API.Services;

/// <summary>
/// Calls the Python FastAPI /recognize endpoint to perform OCR on uploaded images.
/// </summary>
public class OcrService : IOcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OcrService> _logger;

    public OcrService(HttpClient httpClient, ILogger<OcrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OcrResultDto?> RecognizePlateAsync(Stream imageStream, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(streamContent, "file", fileName);

            _logger.LogInformation("Sending image '{FileName}' to Python OCR service...", fileName);

            var response = await _httpClient.PostAsync("/recognize", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("OCR service returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return null;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OcrResultDto>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("OCR recognized plate: {Plate} (confidence: {Confidence})",
                result?.PlateNumber, result?.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with OCR service");
            return null;
        }
    }
}
