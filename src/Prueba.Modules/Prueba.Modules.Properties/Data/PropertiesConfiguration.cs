using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Properties.Data;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.Location)
            .HasMaxLength(500);

        builder.Property(p => p.Address)
            .HasMaxLength(500);

        builder.Property(p => p.City)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Country)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.PricePerNight)
            .HasPrecision(18, 2);

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => new { p.TenantId, p.City });
        builder.HasIndex(p => new { p.TenantId, p.Country });
        builder.HasIndex(p => new { p.TenantId, p.OwnerId });
    }
}

public class PropertyImageConfiguration : IEntityTypeConfiguration<PropertyImage>
{
    public void Configure(EntityTypeBuilder<PropertyImage> builder)
    {
        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(pi => pi.Property)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pi => pi.PropertyId);
    }
}

public class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Price)
            .HasPrecision(18, 2);

        builder.HasOne(a => a.Property)
            .WithMany(p => p.Availability)
            .HasForeignKey(a => a.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Critical for overlap queries and date filtering
        builder.HasIndex(a => new { a.PropertyId, a.Date }).IsUnique();
        builder.HasIndex(a => new { a.PropertyId, a.IsAvailable });
    }
}
