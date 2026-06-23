using Prueba.Domain.Entities;

namespace Prueba.Modules.Properties.Entities;

public class PropertyImage : BaseEntity
{
    public Guid PropertyId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    public Property Property { get; private set; } = null!;

    private PropertyImage() { } // EF Core

    public static PropertyImage Create(Guid propertyId, string url, bool isPrimary, Guid tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (propertyId == Guid.Empty)
            throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        return new PropertyImage
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            Url = url.Trim(),
            IsPrimary = isPrimary,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
