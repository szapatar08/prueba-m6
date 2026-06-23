namespace Prueba.Modules.Dashboard.Features.GetRevenue;

public record GetRevenueQuery(
    Guid PropertyId,
    DateOnly StartDate,
    DateOnly EndDate);

public record RevenueResponse(
    Guid PropertyId,
    string PropertyName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    int BookingCount);
