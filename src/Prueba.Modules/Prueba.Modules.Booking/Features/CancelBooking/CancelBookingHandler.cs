using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.CancelBooking;

public class CancelBookingHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public CancelBookingHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<CancelBookingResponse>> Handle(
        Guid bookingId,
        Guid guestId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var booking = await _repository.GetByIdAsync<BookingEntity>(bookingId, cancellationToken);

        if (booking is null || booking.TenantId != tenantId)
            return Result<CancelBookingResponse>.Fail("Booking not found.");

        if (booking.GuestId != guestId)
            return Result<CancelBookingResponse>.Fail("You can only cancel your own bookings.");

        try
        {
            booking.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return Result<CancelBookingResponse>.Fail(ex.Message);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<CancelBookingResponse>.Success(new CancelBookingResponse(
            booking.Id,
            booking.Status,
            booking.UpdatedAt!.Value));
    }
}

public record CancelBookingResponse(
    Guid Id,
    BookingStatus Status,
    DateTime UpdatedAt);
