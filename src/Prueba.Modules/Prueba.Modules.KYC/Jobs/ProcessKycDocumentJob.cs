using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Jobs;

[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 60 })]
public class ProcessKycDocumentJob
{
    private readonly IRepository _repository;
    private readonly IOcrService _ocrService;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<ProcessKycDocumentJob> _logger;
    private const string BucketName = "kyc-documents";
    private const double ConfidenceThreshold = 80.0;

    public ProcessKycDocumentJob(
        IRepository repository,
        IOcrService ocrService,
        IObjectStorage objectStorage,
        ILogger<ProcessKycDocumentJob> logger)
    {
        _repository = repository;
        _ocrService = ocrService;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    /// <summary>
    /// Process KYC document using OCR service.
    /// Retrieves document from object storage, extracts text via IOcrService,
    /// evaluates confidence, and approves or rejects the validation.
    /// </summary>
    public async Task ExecuteAsync(
        Guid validationId,
        CancellationToken cancellationToken)
    {
        var validation = await _repository.Query<KycValidation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Id == validationId, cancellationToken);

        if (validation is null)
        {
            _logger.LogWarning("KYC validation {ValidationId} not found — skipping", validationId);
            return;
        }

        if (validation.Status != KycStatus.Pending)
        {
            _logger.LogInformation(
                "KYC validation {ValidationId} already in {Status} — idempotent skip",
                validationId, validation.Status);
            return;
        }

        // Retrieve associated document
        var document = await _repository.Query<KycDocument>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.KycValidationId == validationId, cancellationToken);

        if (document is null)
        {
            _logger.LogError("No document found for KYC validation {ValidationId}", validationId);
            validation.Reject("No document found for processing");
            await _repository.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            // Download document from object storage
            await using var documentStream = await _objectStorage.DownloadAsync(
                BucketName, document.StoragePath, cancellationToken);

            // Extract text via OCR
            var ocrResult = await _ocrService.ExtractDocumentDataAsync(documentStream, cancellationToken);

            if (!string.IsNullOrEmpty(ocrResult.ErrorMessage))
            {
                _logger.LogWarning(
                    "OCR returned error for validation {ValidationId}: {Error}",
                    validationId, ocrResult.ErrorMessage);
            }

            // Evaluate confidence threshold
            if (ocrResult.ConfidenceScore * 100 >= ConfidenceThreshold)
            {
                var fullName = $"{ocrResult.Names} {ocrResult.Surnames}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = "Unknown";
                }

                validation.Approve(
                    fullName,
                    ocrResult.DocumentNumber,
                    ocrResult.DateOfBirth ?? DateTime.MinValue,
                    ocrResult.ConfidenceScore * 100);

                _logger.LogInformation(
                    "KYC validation {ValidationId} approved with confidence {Confidence:F1}%",
                    validationId, ocrResult.ConfidenceScore * 100);
            }
            else
            {
                var reason = $"Low confidence: {ocrResult.ConfidenceScore * 100:F1}%. " +
                             $"Required: {ConfidenceThreshold}%. " +
                             (ocrResult.ErrorMessage ?? "Document quality insufficient.");

                validation.Reject(reason);

                _logger.LogInformation(
                    "KYC validation {ValidationId} rejected — confidence {Confidence:F1}% below threshold",
                    validationId, ocrResult.ConfidenceScore * 100);
            }
        }
        catch (OcrException ex)
        {
            // Let Hangfire retry on transient OCR failures
            _logger.LogError(ex, "OCR processing failed for validation {ValidationId}", validationId);
            throw;
        }

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
