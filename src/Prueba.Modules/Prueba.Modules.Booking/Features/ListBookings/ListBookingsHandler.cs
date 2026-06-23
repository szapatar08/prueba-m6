using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.ListBookings;

public class ListBookingsHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public ListBookingsHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<List<BookingResponse>>> Handle(Guid guestId, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var bookings = await _repository.Query<BookingEntity>()
            .Where(b => b.GuestId == guestId && b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingResponse(
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

        return Result<List<BookingResponse>>.Success(bookings);
    }
}

public record BookingResponse(
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
