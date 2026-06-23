using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Features.UploadKycDocument;

public class UploadKycDocumentHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IObjectStorage _objectStorage;

    private const string BucketName = "kyc-documents";

    public UploadKycDocumentHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IObjectStorage objectStorage)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _objectStorage = objectStorage;
    }

    public async Task<Result<UploadKycDocumentResponse>> Handle(
        UploadKycDocumentCommand command,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Check if user already has a pending or approved validation
        var existingValidation = await _repository.Query<KycValidation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                k => k.UserId == userId
                     && k.TenantId == _currentTenant.TenantId!.Value
                     && (k.Status == KycStatus.Pending || k.Status == KycStatus.Approved),
                cancellationToken);

        if (existingValidation is not null && existingValidation.Status == KycStatus.Approved)
        {
            return Result<UploadKycDocumentResponse>.Fail(
                "KYC already approved. No need to upload additional documents.");
        }

        if (existingValidation is not null && existingValidation.Status == KycStatus.Pending)
        {
            return Result<UploadKycDocumentResponse>.Fail(
                "KYC verification already in progress. Please wait for the current verification to complete.");
        }

        var tenantId = _currentTenant.TenantId!.Value;

        // Create KYC validation record
        var validation = KycValidation.Create(
            userId,
            command.ContentType,
            tenantId);

        // Generate non-guessable storage path using GUID
        var storagePath = $"{tenantId}/{userId}/{Guid.NewGuid()}_{command.FileName}";

        // Upload document to MinIO with SSE
        await _objectStorage.UploadAsync(
            BucketName,
            storagePath,
            command.DocumentStream,
            command.ContentType,
            cancellationToken);

        // Create document record
        var document = KycDocument.Create(
            validation.Id,
            command.FileName,
            command.ContentType,
            storagePath,
            tenantId);

        _repository.Add(validation);
        _repository.Add(document);
        await _repository.SaveChangesAsync(cancellationToken);

        // Enqueue background job for OCR processing
        // Note: Hangfire background job will be enqueued via IBackgroundJobClient
        // in the controller or via domain event handler

        return Result<UploadKycDocumentResponse>.Success(new UploadKycDocumentResponse(
            validation.Id,
            validation.Status,
            validation.CreatedAt));
    }
}

public record UploadKycDocumentResponse(
    Guid ValidationId,
    KycStatus Status,
    DateTime UploadedAt);
