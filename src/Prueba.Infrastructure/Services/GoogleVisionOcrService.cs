using System.Text.RegularExpressions;
using Google.Cloud.Vision.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Services;

public partial class GoogleVisionOcrService : IOcrService
{
    private readonly ImageAnnotatorClient _client;
    private readonly ILogger<GoogleVisionOcrService> _logger;

    public GoogleVisionOcrService(ImageAnnotatorClient client, ILogger<GoogleVisionOcrService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<OcrResult> ExtractDocumentDataAsync(Stream imageStream, CancellationToken ct)
    {
        try
        {
            var image = await Image.FromStreamAsync(imageStream);

            var response = await _client.DetectTextAsync(image);

            if (response is null || response.Count == 0)
            {
                _logger.LogWarning("OCR returned no text annotations for document");
                return new OcrResult(
                    string.Empty, string.Empty, string.Empty, null,
                    0.0, "Document was unreadable — no text detected");
            }

            // The first annotation is the full text block
            var fullText = response[0].Description ?? string.Empty;

            var (names, namesConfidence) = ExtractNames(fullText);
            var (surnames, surnamesConfidence) = ExtractSurnames(fullText);
            var (docNumber, docConfidence) = ExtractDocumentNumber(fullText);
            var (dob, dobConfidence) = ExtractDateOfBirth(fullText);

            var averageConfidence = (namesConfidence + surnamesConfidence + docConfidence + dobConfidence) / 4.0;

            return new OcrResult(
                names,
                surnames,
                docNumber,
                dob,
                averageConfidence,
                null);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable ||
                                       ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogError(ex, "Google Cloud Vision API timeout or unavailable");
            throw new OcrException($"OCR service unavailable: {ex.Status.Detail}", ex);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
        {
            _logger.LogError(ex, "Google Cloud Vision API rate limit exceeded");
            throw new OcrException("OCR rate limit exceeded. Please try again later.", ex);
        }
        catch (OcrException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OCR processing");
            throw new OcrException($"OCR processing failed: {ex.Message}", ex);
        }
    }

    internal static (string Value, double Confidence) ExtractNames(string text)
    {
        // Common patterns: "Nombres: Juan Carlos", "Given Name(s): Juan Carlos", "Name: Juan"
        var match = NamesPattern().Match(text);
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), 0.85);
        }

        return (string.Empty, 0.0);
    }

    internal static (string Value, double Confidence) ExtractSurnames(string text)
    {
        // Common patterns: "Apellidos: García López", "Surname: García", "Last Name: García"
        var match = SurnamesPattern().Match(text);
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), 0.85);
        }

        return (string.Empty, 0.0);
    }

    internal static (string Value, double Confidence) ExtractDocumentNumber(string text)
    {
        // Common patterns: "No. Documento: 12345678", "Document No: 12345678", "DNI: 12345678"
        var match = DocNumberPattern().Match(text);
        if (match.Success)
        {
            return (match.Groups[1].Value.Trim(), 0.90);
        }

        // Fallback: look for standalone sequences of 6+ digits that look like document numbers
        var fallbackMatch = FallbackDocNumberPattern().Match(text);
        if (fallbackMatch.Success)
        {
            return (fallbackMatch.Groups[1].Value.Trim(), 0.60);
        }

        return (string.Empty, 0.0);
    }

    internal static (DateTime? Value, double Confidence) ExtractDateOfBirth(string text)
    {
        // Try common date formats
        var match = DobPattern().Match(text);
        if (match.Success)
        {
            var dateStr = match.Groups[1].Value.Trim();
            if (DateTime.TryParse(dateStr, out var parsedDate))
            {
                return (parsedDate, 0.88);
            }
        }

        // Try DD/MM/YYYY or DD-MM/YYYY patterns
        var altMatch = AltDobPattern().Match(text);
        if (altMatch.Success)
        {
            var day = altMatch.Groups[1].Value;
            var month = altMatch.Groups[2].Value;
            var year = altMatch.Groups[3].Value;
            if (DateTime.TryParse($"{year}-{month}-{day}", out var parsedDate))
            {
                return (parsedDate, 0.80);
            }
        }

        return (null, 0.0);
    }

    // Regex patterns — compiled for performance
    [GeneratedRegex(@"(?:Nombres?|Given\s*Name[s]?|Name)\s*[:\-]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NamesPattern();

    [GeneratedRegex(@"(?:Apellidos?|Surname[s]?|Last\s*Name)\s*[:\-]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SurnamesPattern();

    [GeneratedRegex(@"(?:No\.?\s*Documento|Document\s*No|DNI|ID\s*No|C[eé]dula)\s*[:\-]?\s*(\w[\w\-]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DocNumberPattern();

    [GeneratedRegex(@"\b(\d{6,12})\b", RegexOptions.Compiled)]
    private static partial Regex FallbackDocNumberPattern();

    [GeneratedRegex(@"(?:Date\s*of\s*Birth|DOB|Fecha\s*de\s*Nacimiento|Nacimiento)\s*[:\-]\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DobPattern();

    [GeneratedRegex(@"\b(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\b", RegexOptions.Compiled)]
    private static partial Regex AltDobPattern();
}
