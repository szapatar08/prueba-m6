using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.KYC.Entities;

namespace Prueba.Modules.KYC.Features.GetKycStatus;

public class GetKycStatusHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetKycStatusHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<KycStatusResponse>> Handle(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var validation = await _repository.Query<KycValidation>()
            .IgnoreQueryFilters()
            .Where(k => k.UserId == userId && k.TenantId == _currentTenant.TenantId!.Value)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (validation is null)
        {
            return Result<KycStatusResponse>.Success(new KycStatusResponse(
                null,
                KycStatus.Pending,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        return Result<KycStatusResponse>.Success(new KycStatusResponse(
            validation.Id,
            validation.Status,
            validation.DocumentType,
            validation.ExtractedNames,
            validation.ExtractedDocumentNumber,
            validation.ProcessedAt,
            validation.ConfidenceScore,
            validation.ExtractionErrors));
    }
}

public record KycStatusResponse(
    Guid? ValidationId,
    KycStatus Status,
    string? DocumentType,
    string? ExtractedNames,
    string? ExtractedDocumentNumber,
    DateTime? ProcessedAt,
    double? ConfidenceScore,
    string? ExtractionErrors);
