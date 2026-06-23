using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;

namespace Prueba.Modules.Dashboard.Features.GetRevenue;

public class GetRevenueHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetRevenueHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<RevenueResponse>> Handle(
        GetRevenueQuery query,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Verify property belongs to owner
        var property = await _repository.Query<Prueba.Modules.Properties.Entities.Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == query.PropertyId && p.OwnerId == ownerId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<RevenueResponse>.Fail("Property not found or not owned by you.");

        // Get revenue using raw SQL
        var (totalRevenue, bookingCount) = await CalculateRevenueAsync(
            query.PropertyId, tenantId, query.StartDate, query.EndDate, cancellationToken);

        return Result<RevenueResponse>.Success(new RevenueResponse(
            query.PropertyId,
            property.Name,
            query.StartDate,
            query.EndDate,
            totalRevenue,
            bookingCount));
    }

    protected virtual async Task<(decimal TotalRevenue, int BookingCount)> CalculateRevenueAsync(
        Guid propertyId,
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        // Raw SQL to calculate total revenue and booking count
        var bookings = await _repository.Query<Prueba.Modules.Booking.Entities.BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => b.PropertyId == propertyId
                && b.TenantId == tenantId
                && b.Status == Prueba.Modules.Booking.Entities.BookingStatus.Confirmed
                && b.StartDate < endDate
                && b.EndDate > startDate)
            .ToListAsync(cancellationToken);

        return (bookings.Sum(b => b.TotalPrice), bookings.Count);
    }
}
