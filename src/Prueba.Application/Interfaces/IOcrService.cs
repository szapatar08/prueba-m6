namespace Prueba.Application.Interfaces;

public interface IOcrService
{
    Task<OcrResult> ExtractDocumentDataAsync(Stream imageStream, CancellationToken ct);
}

public record OcrResult(
    string Names,
    string Surnames,
    string DocumentNumber,
    DateTime? DateOfBirth,
    double ConfidenceScore,
    string? ErrorMessage);

public class OcrException : Exception
{
    public OcrException(string message) : base(message) { }
    public OcrException(string message, Exception innerException) : base(message, innerException) { }
}
