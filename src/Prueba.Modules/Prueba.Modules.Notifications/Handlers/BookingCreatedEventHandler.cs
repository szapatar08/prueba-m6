using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Notifications.Handlers;

public class BookingCreatedEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _templateRenderer;

    public BookingCreatedEventHandler(
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

    public async Task HandleAsync(
        Guid bookingId,
        Guid guestId,
        Guid propertyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        // Resolve guest email from user entity (fixes hardcoded guest@example.com)
        var guest = await _repository.GetByIdAsync<User>(guestId, cancellationToken)
            ?? throw new InvalidOperationException($"Guest user {guestId} not found.");

        // Load booking and property for template data
        var booking = await _repository.GetByIdAsync<BookingEntity>(bookingId, cancellationToken)
            ?? throw new InvalidOperationException($"Booking {bookingId} not found.");

        var property = await _repository.GetByIdAsync<Property>(propertyId, cancellationToken);

        // Render template and send email
        var templateData = new
        {
            GuestName = $"{guest.FirstName} {guest.LastName}",
            PropertyName = property?.Name ?? "Your property",
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            CheckInTime = booking.CheckInTime.ToString("h:mm tt"),
        };

        var (subject, body) = _templateRenderer.Render(TemplateTypes.BookingCreated, templateData);

        await _emailService.SendEmailAsync(guest.Email, subject, body, cancellationToken);
    }
}
