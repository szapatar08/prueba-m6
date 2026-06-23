using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Entities;

namespace Prueba.Modules.Notifications.Handlers;

public class KycCompletedEventHandler
{
    private readonly IRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailService _emailService;

    public KycCompletedEventHandler(
        IRepository repository,
        ICurrentTenant currentTenant,
        IEmailService emailService)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _emailService = emailService;
    }

    public async Task HandleAsync(Guid userId, string status, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId ?? Guid.Empty;

        var title = status == "Approved"
            ? "KYC Verification Approved"
            : "KYC Verification Rejected";

        var message = status == "Approved"
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

        // Send email notification
        await _emailService.SendEmailAsync(
            "user@example.com", // In production, resolve from user entity
            title,
            $"<h1>{title}</h1><p>{message}</p>",
            cancellationToken);
    }
}
