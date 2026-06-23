using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Jobs;

public class KycCleanupJob
{
    private readonly IRepository _repository;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<KycCleanupJob> _logger;

    private const string BucketName = "kyc-documents";
    private const int RetentionDays = 90;

    public KycCleanupJob(
        IRepository repository,
        IObjectStorage objectStorage,
        ILogger<KycCleanupJob> logger)
    {
        _repository = repository;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    /// <summary>
    /// Cleanup KYC documents older than 90 days after processing.
    /// This job runs daily via Hangfire recurring job.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

        // Find processed validations older than 90 days
        var expiredValidations = await _repository.Query<KycValidation>()
            .IgnoreQueryFilters()
            .Where(k => k.ProcessedAt != null && k.ProcessedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        foreach (var validation in expiredValidations)
        {
            // Find associated documents
            var documents = await _repository.Query<KycDocument>()
                .IgnoreQueryFilters()
                .Where(d => d.KycValidationId == validation.Id)
                .ToListAsync(cancellationToken);

            foreach (var document in documents)
            {
                try
                {
                    // Delete from MinIO
                    await _objectStorage.DeleteAsync(
                        BucketName,
                        document.StoragePath,
                        cancellationToken);

                    // Remove document record
                    _repository.Remove(document);

                    _logger.LogInformation(
                        "Deleted KYC document {DocumentId} for validation {ValidationId}",
                        document.Id,
                        validation.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to delete KYC document {DocumentId} for validation {ValidationId}",
                        document.Id,
                        validation.Id);
                }
            }

            await _repository.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "KYC cleanup completed. Processed {Count} expired validations",
            expiredValidations.Count);
    }
}
