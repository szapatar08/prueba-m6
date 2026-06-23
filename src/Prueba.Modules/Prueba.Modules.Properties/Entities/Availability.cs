using Prueba.Domain.Entities;

namespace Prueba.Modules.Properties.Entities;

public class Availability : BaseEntity
{
    public Guid PropertyId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsAvailable { get; private set; }
    public decimal Price { get; private set; }

    public Property Property { get; private set; } = null!;

    private Availability() { } // EF Core

    public static Availability Create(Guid propertyId, DateOnly date, bool isAvailable, decimal price, Guid tenantId)
    {
        if (propertyId == Guid.Empty)
            throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be positive.");

        return new Availability
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            Date = date,
            IsAvailable = isAvailable,
            Price = price,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkUnavailable()
    {
        IsAvailable = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAvailable()
    {
        IsAvailable = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
