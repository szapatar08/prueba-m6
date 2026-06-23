using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Prueba.Application.Interfaces;

namespace Prueba.Infrastructure.Services;

public class MailKitEmailService : IEmailService
{
    private readonly string _gmailAddress;
    private readonly string _gmailAppPassword;

    public MailKitEmailService(IConfiguration configuration)
    {
        _gmailAddress = Environment.GetEnvironmentVariable("GMAIL_ADDRESS")
            ?? configuration["Gmail:Address"]
            ?? throw new InvalidOperationException("Gmail address not configured. Set GMAIL_ADDRESS environment variable or Gmail:Address in configuration.");

        _gmailAppPassword = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD")
            ?? configuration["Gmail:AppPassword"]
            ?? throw new InvalidOperationException("Gmail app password not configured. Set GMAIL_APP_PASSWORD environment variable or Gmail:AppPassword in configuration.");
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Prueba Platform", _gmailAddress));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = body
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_gmailAddress, _gmailAppPassword, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
