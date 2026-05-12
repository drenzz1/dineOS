namespace DineOS.Application.Interfaces.Services;

public interface IEmailSender
{
    /// <summary>
    /// Sends a plain-text email. The implementation also accepts <paramref name="htmlBody"/>
    /// to attach an HTML alternative when present (multipart/alternative).
    /// </summary>
    Task SendAsync(
        string toAddress,
        string subject,
        string textBody,
        string? htmlBody = null,
        CancellationToken ct = default);
}
