using Prueba.Application.Interfaces;

namespace Prueba.Modules.Notifications.Handlers;

public class BookingCreatedEventHandler
{
    private readonly IEmailService _emailService;

    public BookingCreatedEventHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task HandleAsync(
        Guid bookingId,
        Guid guestId,
        Guid propertyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        // Send confirmation email to guest
        await _emailService.SendEmailAsync(
            "guest@example.com", // In production, resolve from user entity
            "Booking Created - Pending Confirmation",
            $"""
            <h1>Booking Created</h1>
            <p>Your booking {bookingId} has been created and is pending confirmation.</p>
            <ul>
                <li>Check-in: {startDate:yyyy-MM-dd} at 2:00 PM</li>
                <li>Check-out: {endDate:yyyy-MM-dd} at 12:00 PM</li>
            </ul>
            <p>You will receive another email once the owner confirms your booking.</p>
            """,
            cancellationToken);
    }
}
