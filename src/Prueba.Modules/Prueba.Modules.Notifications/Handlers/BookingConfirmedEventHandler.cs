using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Modules.Notifications.Handlers;

public class BookingConfirmedEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;

    public BookingConfirmedEventHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IEmailService emailService)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _emailService = emailService;
    }

    public async Task HandleAsync(Guid bookingId, Guid guestId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        // Resolve guest email from user entity
        var guest = await _repository.GetByIdAsync<User>(guestId, cancellationToken)
            ?? throw new InvalidOperationException($"Guest user {guestId} not found.");

        // Create in-app notification
        var notification = Notification.Create(
            guestId,
            "BookingConfirmed",
            "Booking Confirmed",
            $"Your booking {bookingId} has been confirmed. Check-in is at 2:00 PM.",
            tenantId);

        _repository.Add(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        // Send email notification to resolved guest email
        await _emailService.SendEmailAsync(
            guest.Email,
            "Booking Confirmed",
            $"<h1>Booking Confirmed</h1><p>Your booking {bookingId} has been confirmed. Check-in is at 2:00 PM.</p>",
            cancellationToken);
    }
}
