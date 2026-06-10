using DineOS.Application.Interfaces.Services;
using DineOS.Application.Notifications;
using DineOS.Application.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire job: issues a password-reset code for the given email, renders
/// the Razor template, and sends the email. The Keycloak account lookup
/// happens HERE rather than in the request path so the forgot-password
/// endpoint returns the same constant response whether or not an account
/// exists — the response cannot be used to enumerate emails.
/// </summary>
public sealed class PasswordResetEmailJob(
    IKeycloakAdminClient keycloakAdmin,
    IEmailVerificationService verificationService,
    IEmailSender emailSender,
    IEmailTemplateRenderer templates,
    IOptions<EmailVerificationOptions> verificationOptions,
    ILogger<PasswordResetEmailJob> logger) : IEmailJob
{
    public const string Subject = "Reset your DineOS password";

    [AutomaticRetry(
        Attempts = 3,
        DelaysInSeconds = new[] { 10, 30, 90 },
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendAsync(string email, CancellationToken ct)
    {
        var user = await keycloakAdmin.FindUserByEmailAsync(email, ct);
        if (user is null)
        {
            // The endpoint already returned its constant response; sending
            // nothing is the correct outcome for an unknown address.
            logger.LogInformation("Password reset email skipped — no account matches the requested email.");
            return;
        }

        var code = await verificationService.IssuePasswordResetCodeAsync(email, ct);

        var model = new PasswordResetEmailModel(
            Email:          email,
            Code:           code,
            CodeTtlMinutes: verificationOptions.Value.CodeTtlMinutes);

        var html = await templates.RenderAsync("PasswordReset", model, ct);
        var text = $"""
                    Hi,

                    We received a request to reset the DineOS password for {email}.
                    Your reset code:

                        {code}

                    The code expires in {verificationOptions.Value.CodeTtlMinutes} minutes.
                    If you didn't request a password reset, you can ignore this
                    email — your password is unchanged.
                    """;

        await emailSender.SendAsync(email, Subject, text, html, ct);
    }
}
