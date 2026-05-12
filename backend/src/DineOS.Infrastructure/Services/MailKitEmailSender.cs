using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DineOS.Infrastructure.Services;

public sealed class MailKitEmailSender(
    IOptions<EmailOptions> emailOptions,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _email = emailOptions.Value;
    private readonly SmtpOptions _smtp = smtpOptions.Value;

    public async Task SendAsync(
        string toAddress,
        string subject,
        string textBody,
        string? htmlBody = null,
        CancellationToken ct = default)
    {
        if (_email.SimulateFailure)
            throw new InvalidOperationException(
                "Email:SimulateFailure is enabled — refusing to send so the retry/DLQ pipeline can be exercised.");

        if (!_email.Enabled)
        {
            logger.LogInformation(
                "Email delivery disabled. Would have sent to {To} with subject {Subject}",
                toAddress, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_email.FromName, _email.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { TextBody = textBody };
        if (!string.IsNullOrWhiteSpace(htmlBody))
            bodyBuilder.HtmlBody = htmlBody;
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_smtp.Host, _smtp.Port, socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(_smtp.Username))
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation(
            "Email sent: To={To} Subject={Subject} From={From} Html={Html}",
            toAddress, subject, _email.FromAddress, htmlBody is not null);
    }
}
