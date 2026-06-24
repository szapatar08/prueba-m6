using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.Booking.Entities;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Services;
using Prueba.Modules.Properties.Entities;

namespace Prueba.Modules.Notifications.Jobs;

public class SendDepartureRemindersJob
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailService _emailService;
    private readonly ILogger<SendDepartureRemindersJob> _logger;

    public SendDepartureRemindersJob(
        IRepository repository,
        ICurrentTenant currentTenant,
        IEmailTemplateRenderer templateRenderer,
        IEmailService emailService,
        ILogger<SendDepartureRemindersJob> logger)
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var bookings = _repository.Query<BookingEntity>()
            .Where(b => b.EndDate == today && b.Status == BookingStatus.Confirmed)
            .ToList();

        if (bookings.Count == 0)
        {
            _logger.LogInformation("No departure reminders to send for {Date}.", today);
            return;
        }

        _logger.LogInformation("Processing {Count} departure reminders for {Date}.", bookings.Count, today);

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
                    EndDate = booking.EndDate.ToString("yyyy-MM-dd"),
                    CheckOutTime = booking.CheckOutTime.ToString("h:mm tt"),
                    Instructions = "Please ensure all personal belongings are collected before check-out. Keys should be returned to the designated location.",
                };

                var (subject, body) = _templateRenderer.Render(TemplateTypes.DepartureReminder, templateData);

                await _emailService.SendEmailAsync(guest.Email, subject, body, cancellationToken);

                _logger.LogInformation("Sent departure reminder for booking {BookingId} to {Email}.", booking.Id, guest.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send departure reminder for booking {BookingId}.", booking.Id);
            }
        }
    }
}
