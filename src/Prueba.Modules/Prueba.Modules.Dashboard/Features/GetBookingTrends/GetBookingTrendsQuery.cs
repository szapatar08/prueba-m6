namespace Prueba.Modules.Dashboard.Features.GetBookingTrends;

public enum TrendPeriod
{
    Weekly,
    Monthly
}

public record GetBookingTrendsQuery(
    Guid PropertyId,
    TrendPeriod Period);

public record BookingTrendsResponse(
    Guid PropertyId,
    string PropertyName,
    TrendPeriod Period,
    List<TrendDataPoint> Trends);

public record TrendDataPoint(
    string Period,
    int BookingCount,
    decimal Revenue);
