using Prueba.Domain.Entities;
using Prueba.Modules.Booking.Events;

namespace Prueba.Modules.Booking.Entities;

public class BookingEntity : AggregateRoot
{
    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public BookingStatus Status { get; private set; }
    public decimal TotalPrice { get; private set; }
    public TimeOnly CheckInTime { get; private set; }
    public TimeOnly CheckOutTime { get; private set; }

    private BookingEntity() { } // EF Core

    public static BookingEntity Create(
        Guid propertyId,
        Guid guestId,
        DateOnly startDate,
        DateOnly endDate,
        decimal totalPrice,
        Guid tenantId)
    {
        if (propertyId == Guid.Empty)
            throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (guestId == Guid.Empty)
            throw new ArgumentException("Guest ID cannot be empty.", nameof(guestId));
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (endDate <= startDate)
            throw new ArgumentException("End date must be after start date.", nameof(endDate));
        if (totalPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalPrice), "Total price must be positive.");

        return new BookingEntity
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            GuestId = guestId,
            StartDate = startDate,
            EndDate = endDate,
            Status = BookingStatus.Pending,
            TotalPrice = totalPrice,
            CheckInTime = new TimeOnly(14, 0),  // 2:00 PM
            CheckOutTime = new TimeOnly(12, 0), // 12:00 PM
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Confirm()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot confirm booking in {Status} status. Only Pending bookings can be confirmed.");

        Status = BookingStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new BookingConfirmed(Id));
    }

    public void Cancel()
    {
        if (Status is BookingStatus.Cancelled or BookingStatus.Completed)
            throw new InvalidOperationException(
                $"Cannot cancel booking in {Status} status.");

        Status = BookingStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new BookingCancelled(Id));
    }

    public void Complete()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException(
                $"Cannot complete booking in {Status} status. Only Confirmed bookings can be completed.");

        Status = BookingStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
