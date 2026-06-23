using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Booking.Features.CheckAvailability;

public class CheckAvailabilityHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public CheckAvailabilityHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AvailabilityResponse>> Handle(
        Guid propertyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        var hasConflict = await _repository.Query<BookingEntity>()
            .Where(b => b.PropertyId == propertyId
                && b.TenantId == tenantId
                && b.Status == BookingStatus.Confirmed
                && b.StartDate < endDate
                && b.EndDate > startDate)
            .AnyAsync(cancellationToken);

        return Result<AvailabilityResponse>.Success(new AvailabilityResponse(
            PropertyId: propertyId,
            StartDate: startDate,
            EndDate: endDate,
            IsAvailable: !hasConflict));
    }
}

public record AvailabilityResponse(
    Guid PropertyId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsAvailable);
