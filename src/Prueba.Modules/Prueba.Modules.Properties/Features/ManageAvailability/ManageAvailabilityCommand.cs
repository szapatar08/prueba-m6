namespace Prueba.Modules.Properties.Features.ManageAvailability;

public record SetAvailabilityCommand(
    DateOnly Date,
    bool IsAvailable,
    decimal Price);

public record SetAvailabilityRangeCommand(
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsAvailable,
    decimal Price);
