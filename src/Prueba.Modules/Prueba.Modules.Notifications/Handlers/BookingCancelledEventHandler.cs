using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Modules.Notifications.Handlers;

public class BookingCancelledEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;

    public BookingCancelledEventHandler(
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

        // Create in-app notification
        var notification = Notification.Create(
            guestId,
            "BookingCancelled",
            "Booking Cancelled",
            $"Your booking {bookingId} has been cancelled.",
            tenantId);

        _repository.Add(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        // Send email notification
        await _emailService.SendEmailAsync(
            "guest@example.com", // In production, resolve from user entity
            "Booking Cancelled",
            $"<h1>Booking Cancelled</h1><p>Your booking {bookingId} has been cancelled.</p>",
            cancellationToken);
    }
}
