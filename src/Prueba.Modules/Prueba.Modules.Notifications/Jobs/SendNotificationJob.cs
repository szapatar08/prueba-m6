using Hangfire;
using Microsoft.Extensions.Logging;
using Prueba.Application.Interfaces;
using Prueba.Modules.Notifications.Handlers;

namespace Prueba.Modules.Notifications.Jobs;

public class SendNotificationJob
{
    private readonly IEmailService _emailService;
    private readonly ILogger<SendNotificationJob> _logger;

    public SendNotificationJob(
        IEmailService emailService,
        ILogger<SendNotificationJob> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ExecuteAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sending notification email to {Recipient}", to);
            await _emailService.SendEmailAsync(to, subject, body, cancellationToken);
            _logger.LogInformation("Successfully sent notification email to {Recipient}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Recipient}", to);
            throw; // Let Hangfire handle retry
        }
    }
}
