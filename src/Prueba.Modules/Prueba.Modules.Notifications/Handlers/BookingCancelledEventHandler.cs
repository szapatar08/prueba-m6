using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Notifications.Handlers;

public class BookingCancelledEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _templateRenderer;

    public BookingCancelledEventHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IEmailService emailService,
        IEmailTemplateRenderer templateRenderer)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _emailService = emailService;
        _templateRenderer = templateRenderer;
    }

    public async Task HandleAsync(Guid bookingId, Guid guestId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        // Resolve guest email from user entity (fixes hardcoded guest@example.com)
        var guest = await _repository.GetByIdAsync<User>(guestId, cancellationToken)
            ?? throw new InvalidOperationException($"Guest user {guestId} not found.");

        // Load booking and property for template data
        var booking = await _repository.GetByIdAsync<BookingEntity>(bookingId, cancellationToken);

        var property = booking is not null
            ? await _repository.GetByIdAsync<Property>(booking.PropertyId, cancellationToken)
            : null;

        // Create in-app notification
        var notification = Notification.Create(
            guestId,
            "BookingCancelled",
            "Booking Cancelled",
            $"Your booking {bookingId} has been cancelled.",
            tenantId);

        _repository.Add(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        // Render template and send email
        var templateData = new
        {
            GuestName = $"{guest.FirstName} {guest.LastName}",
            PropertyName = property?.Name ?? "Your property",
            StartDate = booking?.StartDate.ToString("yyyy-MM-dd") ?? "",
            EndDate = booking?.EndDate.ToString("yyyy-MM-dd") ?? "",
            RefundInfo = "Please contact support for refund details.",
        };

        var (subject, body) = _templateRenderer.Render(TemplateTypes.BookingCancelled, templateData);

        await _emailService.SendEmailAsync(guest.Email, subject, body, cancellationToken);
    }
}
