namespace Prueba.Modules.Dashboard.Features.GetOccupancyRate;

public record GetOccupancyRateQuery(
    Guid PropertyId,
    DateOnly StartDate,
    DateOnly EndDate);

public record OccupancyRateResponse(
    Guid PropertyId,
    string PropertyName,
    DateOnly StartDate,
    DateOnly EndDate,
    int TotalDays,
    int BookedDays,
    decimal OccupancyRate);
