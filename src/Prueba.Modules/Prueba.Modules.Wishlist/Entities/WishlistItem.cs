using Prueba.Domain.Entities;

namespace Prueba.Modules.Wishlist.Entities;

public class WishlistItem : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid PropertyId { get; private set; }

    private WishlistItem() { } // EF Core

    public static WishlistItem Create(
        Guid userId,
        Guid propertyId,
        Guid tenantId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (propertyId == Guid.Empty)
            throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));

        return new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PropertyId = propertyId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
