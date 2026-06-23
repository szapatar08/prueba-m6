using Microsoft.EntityFrameworkCore;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Jobs;

public class ProcessKycDocumentJob
{
    private readonly IRepository _repository;

    public ProcessKycDocumentJob(IRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Process KYC document with OCR stub.
    /// In production, this would call an external OCR/AI service.
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
            // Validation not found — job should not retry
            return;
        }

        if (validation.Status != KycStatus.Pending)
        {
            // Already processed — idempotent
            return;
        }

        // Stub OCR: extract mock data
        // In production, this would call an external OCR service
        var extractedNames = "John Doe";
        var extractedDocumentNumber = "DOC-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
        var extractedDateOfBirth = new DateTime(1990, 1, 15);

        // Simulate processing delay
        await Task.Delay(100, cancellationToken);

        // Determine status based on stub logic
        // In production, the OCR service would return confidence scores
        // For the stub, we'll approve all documents
        validation.Approve(
            extractedNames,
            extractedDocumentNumber,
            extractedDateOfBirth);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
