using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.GetPropertyBookings;

public class GetPropertyBookingsHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetPropertyBookingsHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<List<PropertyBookingResponse>>> Handle(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var bookings = await _repository.Query<BookingEntity>()
            .Where(b => b.PropertyId == propertyId && b.TenantId == tenantId)
            .OrderByDescending(b => b.StartDate)
            .Select(b => new PropertyBookingResponse(
                b.Id,
                b.PropertyId,
                b.GuestId,
                b.StartDate,
                b.EndDate,
                b.Status,
                b.TotalPrice,
                b.CheckInTime,
                b.CheckOutTime,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<PropertyBookingResponse>>.Success(bookings);
    }
}

public record PropertyBookingResponse(
    Guid Id,
    Guid PropertyId,
    Guid GuestId,
    DateOnly StartDate,
    DateOnly EndDate,
    BookingStatus Status,
    decimal TotalPrice,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    DateTime CreatedAt);
