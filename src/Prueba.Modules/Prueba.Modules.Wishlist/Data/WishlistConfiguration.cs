using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prueba.Modules.Wishlist.Entities;

namespace Prueba.Modules.Wishlist.Data;

public class WishlistConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.UserId)
            .IsRequired();

        builder.Property(w => w.PropertyId)
            .IsRequired();

        // Unique constraint: a user can only save a property once per tenant
        builder.HasIndex(w => new { w.UserId, w.PropertyId, w.TenantId })
            .IsUnique();

        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.TenantId);
    }
}
