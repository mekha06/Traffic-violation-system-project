using System.Text;
using System.Text.Json;

namespace TrafficViolationSystem.API.Services;

/// <summary>
/// Calls the Google Gemini API to generate AI draft summaries for violation cases.
/// Sends only text data (plate, junction, violation type) — no images.
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GenerateSummaryAsync(string plateNumber, string junctionName,
        string violationType, DateTime violationDate, string? description)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_GEMINI_API_KEY_HERE")
        {
            _logger.LogWarning("Gemini API key is not configured. Returning placeholder summary.");
            return $"[AI Summary Placeholder] Vehicle {plateNumber} was detected committing a " +
                   $"{violationType} violation at {junctionName} on {violationDate:yyyy-MM-dd HH:mm}. " +
                   $"Please configure the Gemini API key to enable AI-generated summaries.";
        }

        try
        {
            var model = _configuration["Gemini:Model"] ?? "gemini-2.0-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var prompt = BuildPrompt(plateNumber, junctionName, violationType, violationDate, description);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 300
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending summary request to Gemini API...");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API returned {StatusCode}: {Error}", response.StatusCode, error);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            // Extract the text from the Gemini response structure:
            // { "candidates": [{ "content": { "parts": [{ "text": "..." }] } }] }
            var summary = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            _logger.LogInformation("Gemini AI summary generated successfully.");
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI summary via Gemini");
            return null;
        }
    }

    private static string BuildPrompt(string plateNumber, string junctionName,
        string violationType, DateTime violationDate, string? description)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a traffic violation report assistant. Generate a brief, professional summary for a traffic officer to review.");
        sb.AppendLine();
        sb.AppendLine("Violation Details:");
        sb.AppendLine($"- Vehicle Plate Number: {plateNumber}");
        sb.AppendLine($"- Junction/Location: {junctionName}");
        sb.AppendLine($"- Violation Type: {violationType}");
        sb.AppendLine($"- Date & Time: {violationDate:yyyy-MM-dd HH:mm:ss} UTC");

        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"- Additional Details: {description}");
        }

        sb.AppendLine();
        sb.AppendLine("Write a 2-3 sentence draft summary suitable for inclusion in an official traffic violation report. Be factual and concise.");

        return sb.ToString();
    }
}
