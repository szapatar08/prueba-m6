using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Data;

public class BookingConfiguration : IEntityTypeConfiguration<BookingEntity>
{
    public void Configure(EntityTypeBuilder<BookingEntity> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.PropertyId)
            .IsRequired();

        builder.Property(b => b.GuestId)
            .IsRequired();

        builder.Property(b => b.StartDate)
            .IsRequired();

        builder.Property(b => b.EndDate)
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.TotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(b => b.CheckInTime)
            .IsRequired();

        builder.Property(b => b.CheckOutTime)
            .IsRequired();

        // Critical indexes for overlap queries and filtering
        builder.HasIndex(b => new { b.PropertyId, b.StartDate, b.EndDate, b.Status });
        builder.HasIndex(b => b.GuestId);
        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => b.Status);
    }
}
