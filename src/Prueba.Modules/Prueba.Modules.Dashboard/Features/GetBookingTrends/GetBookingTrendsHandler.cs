using Microsoft.EntityFrameworkCore;
using Prueba.Application.Common;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;

namespace Prueba.Modules.Dashboard.Features.GetBookingTrends;

public class GetBookingTrendsHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetBookingTrendsHandler(IRepository repository, ICurrentTenant currentTenant)
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<BookingTrendsResponse>> Handle(
        GetBookingTrendsQuery query,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId!.Value;

        // Verify property belongs to owner
        var property = await _repository.Query<Prueba.Modules.Properties.Entities.Property>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == query.PropertyId && p.OwnerId == ownerId && p.TenantId == tenantId, cancellationToken);

        if (property is null)
            return Result<BookingTrendsResponse>.Fail("Property not found or not owned by you.");

        // Get bookings for the last 12 months
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-12));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var bookings = await _repository.Query<BookingEntity>()
            .IgnoreQueryFilters()
            .Where(b => b.PropertyId == query.PropertyId
                && b.TenantId == tenantId
                && b.Status == BookingStatus.Confirmed
                && b.StartDate >= startDate
                && b.EndDate <= endDate)
            .ToListAsync(cancellationToken);

        // Group by period
        var trends = query.Period == TrendPeriod.Monthly
            ? GroupByMonth(bookings)
            : GroupByWeek(bookings);

        return Result<BookingTrendsResponse>.Success(new BookingTrendsResponse(
            query.PropertyId,
            property.Name,
            query.Period,
            trends));
    }

    private static List<TrendDataPoint> GroupByMonth(List<BookingEntity> bookings)
    {
        return bookings
            .GroupBy(b => new { b.StartDate.Year, b.StartDate.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new TrendDataPoint(
                $"{g.Key.Year}-{g.Key.Month:D2}",
                g.Count(),
                g.Sum(b => b.TotalPrice)))
            .ToList();
    }

    private static List<TrendDataPoint> GroupByWeek(List<BookingEntity> bookings)
    {
        return bookings
            .GroupBy(b => System.Globalization.ISOWeek.GetYear(b.StartDate.ToDateTime(TimeOnly.MinValue)))
            .OrderBy(g => g.Key)
            .Select(g => new TrendDataPoint(
                $"Week {g.Key}",
                g.Count(),
                g.Sum(b => b.TotalPrice)))
            .ToList();
    }
}
