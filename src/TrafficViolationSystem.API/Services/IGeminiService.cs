namespace TrafficViolationSystem.API.Services;

/// <summary>
/// Generates AI draft summaries for violation cases using Google Gemini.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates a short AI summary for a violation case.
    /// </summary>
    Task<string?> GenerateSummaryAsync(string plateNumber, string junctionName,
        string violationType, DateTime violationDate, string? description);
}
