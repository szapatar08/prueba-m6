using Prueba.Application.Interfaces;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Notifications.Entities;
using Prueba.Modules.Notifications.Services;

namespace Prueba.Modules.Notifications.Handlers;

public class KycCompletedEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateRenderer _templateRenderer;

    public KycCompletedEventHandler(
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

    public async Task HandleAsync(Guid userId, string status, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        // Resolve user email from entity (fixes hardcoded user@example.com)
        var user = await _repository.GetByIdAsync<User>(userId, cancellationToken)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var isApproved = status == "Approved";
        var title = isApproved
            ? "KYC Verification Approved"
            : "KYC Verification Rejected";

        var message = isApproved
            ? "Your identity verification has been approved. You can now make bookings."
            : "Your identity verification has been rejected. Please upload a valid document.";

        // Create in-app notification
        var notification = Notification.Create(
            userId,
            "KycCompleted",
            title,
            message,
            tenantId);

        _repository.Add(notification);
        await _repository.SaveChangesAsync(cancellationToken);

        // Render template and send email
        var templateType = isApproved ? TemplateTypes.KycApproved : TemplateTypes.KycRejected;

        var templateData = new
        {
            GuestName = $"{user.FirstName} {user.LastName}",
            RejectionReason = isApproved ? null : "Document did not meet verification requirements.",
        };

        var (subject, body) = _templateRenderer.Render(templateType, templateData);

        await _emailService.SendEmailAsync(user.Email, subject, body, cancellationToken);
    }
}
