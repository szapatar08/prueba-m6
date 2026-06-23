using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.ConfirmBooking;

public class ConfirmBookingHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public ConfirmBookingHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ConfirmBookingResponse>> Handle(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var booking = await _repository.GetByIdAsync<BookingEntity>(bookingId, cancellationToken);

        if (booking is null || booking.TenantId != tenantId)
            return Result<ConfirmBookingResponse>.Fail("Booking not found.");

        try
        {
            booking.Confirm();
        }
        catch (InvalidOperationException ex)
        {
            return Result<ConfirmBookingResponse>.Fail(ex.Message);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<ConfirmBookingResponse>.Success(new ConfirmBookingResponse(
            booking.Id,
            booking.Status,
            booking.UpdatedAt!.Value));
    }
}

public record ConfirmBookingResponse(
    Guid Id,
    BookingStatus Status,
    DateTime UpdatedAt);
