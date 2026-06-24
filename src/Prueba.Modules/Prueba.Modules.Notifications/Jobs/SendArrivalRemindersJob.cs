using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Notifications.Jobs;

public class SendArrivalRemindersJob
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailService _emailService;
    private readonly ILogger<SendArrivalRemindersJob> _logger;

    public SendArrivalRemindersJob(
        IRepository repository,
        ICurrentTenant currentTenant,
        IEmailTemplateRenderer templateRenderer,
        IEmailService emailService,
        ILogger<SendArrivalRemindersJob> logger)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _templateRenderer = templateRenderer;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var bookings = _repository.Query<BookingEntity>()
            .Where(b => b.StartDate == tomorrow && b.Status == BookingStatus.Confirmed)
            .ToList();

        if (bookings.Count == 0)
        {
            _logger.LogInformation("No arrival reminders to send for {Date}.", tomorrow);
            return;
        }

        _logger.LogInformation("Processing {Count} arrival reminders for {Date}.", bookings.Count, tomorrow);

        foreach (var booking in bookings)
        {
            try
            {
                var guest = await _repository.GetByIdAsync<User>(booking.GuestId, cancellationToken);
                if (guest is null)
                {
                    _logger.LogWarning("Guest {GuestId} not found for booking {BookingId}. Skipping.", booking.GuestId, booking.Id);
                    continue;
                }

                var property = await _repository.GetByIdAsync<Property>(booking.PropertyId, cancellationToken);

                var templateData = new
                {
                    GuestName = $"{guest.FirstName} {guest.LastName}",
                    PropertyName = property?.Name ?? "Your property",
                    StartDate = booking.StartDate.ToString("yyyy-MM-dd"),
                    CheckInTime = booking.CheckInTime.ToString("h:mm tt"),
                    Address = property?.Address ?? "See booking details",
                    Instructions = "Please arrive during check-in hours. Contact the property for early check-in availability.",
                };

                var (subject, body) = _templateRenderer.Render(TemplateTypes.ArrivalReminder, templateData);

                await _emailService.SendEmailAsync(guest.Email, subject, body, cancellationToken);

                _logger.LogInformation("Sent arrival reminder for booking {BookingId} to {Email}.", booking.Id, guest.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send arrival reminder for booking {BookingId}.", booking.Id);
            }
        }
    }
}
