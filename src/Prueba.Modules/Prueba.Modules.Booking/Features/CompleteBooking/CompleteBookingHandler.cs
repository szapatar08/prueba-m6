using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.CompleteBooking;

public class CompleteBookingHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public CompleteBookingHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<CompleteBookingResponse>> Handle(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var booking = await _repository.GetByIdAsync<BookingEntity>(bookingId, cancellationToken);

        if (booking is null || booking.TenantId != tenantId)
            return Result<CompleteBookingResponse>.Fail("Booking not found.");

        try
        {
            booking.Complete();
        }
        catch (InvalidOperationException ex)
        {
            return Result<CompleteBookingResponse>.Fail(ex.Message);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<CompleteBookingResponse>.Success(new CompleteBookingResponse(
            booking.Id,
            booking.Status,
            booking.UpdatedAt!.Value));
    }
}

public record CompleteBookingResponse(
    Guid Id,
    BookingStatus Status,
    DateTime UpdatedAt);
