namespace Prueba.Modules.Booking.Features.CreateBooking;

public record CreateBookingCommand(
    Guid PropertyId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalPrice);
