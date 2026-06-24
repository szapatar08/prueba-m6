using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;

namespace Prueba.Modules.Notifications.Jobs;

public class SeedNotificationTemplatesJob
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<SeedNotificationTemplatesJob> _logger;

    public SeedNotificationTemplatesJob(
        IRepository repository,
        ICurrentTenant currentTenant,
        ILogger<SeedNotificationTemplatesJob> logger)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        var existingTypesList = _repository.Query<NotificationTemplate>()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Type)
            .ToList();
        var existingTypes = new HashSet<string>(existingTypesList);

        var templatesToSeed = GetTemplateDefinitions()
            .Where(def => !existingTypes.Contains(def.Type))
            .Select(def => NotificationTemplate.Create(def.Type, def.Subject, def.Body, tenantId))
            .ToList();

        if (templatesToSeed.Count == 0)
        {
            _logger.LogInformation("All notification templates already seeded for tenant {TenantId}.", tenantId);
            return;
        }

        foreach (var template in templatesToSeed)
        {
            _repository.Add(template);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} notification templates for tenant {TenantId}.", templatesToSeed.Count, tenantId);
    }

    private static List<TemplateDefinition> GetTemplateDefinitions()
    {
        return
        [
            new TemplateDefinition(
                TemplateTypes.BookingCreated,
                "Booking Created - {{PropertyName}}",
                """
                <html>
                <body>
                    <h1>Booking Created</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>Your booking at <strong>{{PropertyName}}</strong> has been created and is pending confirmation.</p>
                    <ul>
                        <li>Check-in: {{StartDate}} at {{CheckInTime}}</li>
                        <li>Check-out: {{EndDate}}</li>
                    </ul>
                    <p>You will receive another email once the owner confirms your booking.</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.BookingConfirmed,
                "Booking Confirmed - {{PropertyName}}",
                """
                <html>
                <body>
                    <h1>Booking Confirmed</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>Your booking at <strong>{{PropertyName}}</strong> has been confirmed!</p>
                    <ul>
                        <li>Check-in: {{StartDate}} at {{CheckInTime}}</li>
                        <li>Check-out: {{EndDate}}</li>
                        <li>Total: {{TotalPrice}}</li>
                    </ul>
                    <p>We look forward to welcoming you.</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.BookingCancelled,
                "Booking Cancelled - {{PropertyName}}",
                """
                <html>
                <body>
                    <h1>Booking Cancelled</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>Your booking at <strong>{{PropertyName}}</strong> from {{StartDate}} to {{EndDate}} has been cancelled.</p>
                    <p>{{RefundInfo}}</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.KycApproved,
                "Identity Verification Approved",
                """
                <html>
                <body>
                    <h1>Identity Verification Approved</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>Your identity verification has been approved. You can now make bookings on our platform.</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.KycRejected,
                "Identity Verification Rejected",
                """
                <html>
                <body>
                    <h1>Identity Verification Rejected</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>Your identity verification has been rejected.</p>
                    <p><strong>Reason:</strong> {{RejectionReason}}</p>
                    <p>Please upload a valid document and try again.</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.ArrivalReminder,
                "Your check-in is tomorrow - {{PropertyName}}",
                """
                <html>
                <body>
                    <h1>Check-in Reminder</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>This is a friendly reminder that your check-in at <strong>{{PropertyName}}</strong> is tomorrow ({{StartDate}}).</p>
                    <ul>
                        <li>Check-in time: {{CheckInTime}}</li>
                        <li>Address: {{Address}}</li>
                    </ul>
                    <p><strong>Instructions:</strong> {{Instructions}}</p>
                    <p>We look forward to welcoming you!</p>
                </body>
                </html>
                """),

            new TemplateDefinition(
                TemplateTypes.DepartureReminder,
                "Check-out today - {{PropertyName}}",
                """
                <html>
                <body>
                    <h1>Check-out Reminder</h1>
                    <p>Hello {{GuestName}},</p>
                    <p>This is a friendly reminder that your check-out from <strong>{{PropertyName}}</strong> is today ({{EndDate}}).</p>
                    <ul>
                        <li>Check-out time: {{CheckOutTime}}</li>
                    </ul>
                    <p><strong>Instructions:</strong> {{Instructions}}</p>
                    <p>Thank you for staying with us!</p>
                </body>
                </html>
                """),
        ];
    }

    private record TemplateDefinition(string Type, string Subject, string Body);
}
