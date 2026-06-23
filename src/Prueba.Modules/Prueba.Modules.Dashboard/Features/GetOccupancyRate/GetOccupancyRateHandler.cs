using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Dashboard.Features.GetOccupancyRate;

public class GetOccupancyRateHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetOccupancyRateHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<OccupancyRateResponse>> Handle(
        GetOccupancyRateQuery query,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Verify property belongs to owner
        var property = await _repository.Query<Prueba.Modules.Properties.Entities.Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == query.PropertyId && p.OwnerId == ownerId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<OccupancyRateResponse>.Fail("Property not found or not owned by you.");

        // Calculate total days in period
        var totalDays = query.EndDate.DayNumber - query.StartDate.DayNumber;
        if (totalDays <= 0)
            return Result<OccupancyRateResponse>.Fail("End date must be after start date.");

        // Get booked days using raw SQL for accurate calculation
        var bookedDays = await CalculateBookedDaysAsync(
            query.PropertyId, tenantId, query.StartDate, query.EndDate, cancellationToken);

        var occupancyRate = totalDays > 0
            ? Math.Round((decimal)bookedDays / totalDays * 100, 2)
            : 0m;

        return Result<OccupancyRateResponse>.Success(new OccupancyRateResponse(
            query.PropertyId,
            property.Name,
            query.StartDate,
            query.EndDate,
            totalDays,
            bookedDays,
            occupancyRate));
    }

    protected virtual async Task<int> CalculateBookedDaysAsync(
        Guid propertyId,
        Guid tenantId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        // Raw SQL to calculate total booked days in the period
        // For each booking, we calculate the overlap with the query period
        // and sum the days
        var result = await _repository.ExecuteSqlRawAsync(
            """
            SELECT COALESCE(SUM(
                LEAST("EndDate", {3}) - GREATEST("StartDate", {2})
            ), 0)
            FROM "Bookings"
            WHERE "PropertyId" = {0}
              AND "TenantId" = {1}
              AND "Status" = 'Confirmed'
              AND "StartDate" < {3}
              AND "EndDate" > {2}
            """,
            cancellationToken,
            propertyId,
            tenantId,
            startDate,
            endDate);

        return result;
    }
}
