using Prueba.Domain.Entities;
using Prueba.Modules.KYC.Events;

namespace Prueba.Modules.KYC.Entities;

public class KycValidation : AggregateRoot
{
    public Guid UserId { get; private set; }
    public KycStatus Status { get; private set; }
    public string? DocumentType { get; private set; }
    public string? ExtractedNames { get; private set; }
    public string? ExtractedDocumentNumber { get; private set; }
    public DateTime? ExtractedDateOfBirth { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public double? ConfidenceScore { get; private set; }
    public string? ExtractionErrors { get; private set; }

    private KycValidation() { } // EF Core

    public static KycValidation Create(
        Guid userId,
        string documentType,
        Guid tenantId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Document type cannot be empty.", nameof(documentType));

        return new KycValidation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = KycStatus.Pending,
            DocumentType = documentType,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(
        string extractedNames,
        string extractedDocumentNumber,
        DateTime extractedDateOfBirth,
        double confidenceScore)
    {
        if (Status != KycStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve validation in {Status} status. Only Pending validations can be approved.");

        Status = KycStatus.Approved;
        ExtractedNames = extractedNames;
        ExtractedDocumentNumber = extractedDocumentNumber;
        ExtractedDateOfBirth = extractedDateOfBirth;
        ConfidenceScore = confidenceScore;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KycCompleted(Id, UserId, KycStatus.Approved));
    }

    public void Reject(string reason)
    {
        if (Status != KycStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject validation in {Status} status. Only Pending validations can be rejected.");

        Status = KycStatus.Rejected;
        ExtractionErrors = reason;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new KycCompleted(Id, UserId, KycStatus.Rejected));
    }

    public bool IsApproved => Status == KycStatus.Approved;
}
