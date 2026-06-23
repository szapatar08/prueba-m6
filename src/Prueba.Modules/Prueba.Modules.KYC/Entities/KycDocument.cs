using Prueba.Domain.Entities;

namespace Prueba.Modules.KYC.Entities;

public class KycDocument : BaseEntity
{
    public Guid KycValidationId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }

    private KycDocument() { } // EF Core

    public static KycDocument Create(
        Guid kycValidationId,
        string fileName,
        string contentType,
        string storagePath,
        Guid tenantId)
    {
        if (kycValidationId == Guid.Empty)
            throw new ArgumentException("KYC Validation ID cannot be empty.", nameof(kycValidationId));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));

        return new KycDocument
        {
            Id = Guid.NewGuid(),
            KycValidationId = kycValidationId,
            FileName = fileName,
            ContentType = contentType,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
