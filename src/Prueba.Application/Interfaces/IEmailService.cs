namespace Prueba.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
