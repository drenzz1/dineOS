using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Infrastructure.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DineOS.Tests.Unit;

/// <summary>
/// The enumeration-safety contract of the forgot-password flow lives in this
/// job: the endpoint always answers with the same constant response, and the
/// "does this account exist?" decision happens here, out of band.
/// </summary>
public class PasswordResetEmailJobTests
{
    private static (PasswordResetEmailJob job, IKeycloakAdminClient admin, IEmailSender sender, IEmailVerificationService verifier, IEmailTemplateRenderer templates)
        CreateSut()
    {
        var admin     = Substitute.For<IKeycloakAdminClient>();
        var sender    = Substitute.For<IEmailSender>();
        var verifier  = Substitute.For<IEmailVerificationService>();
        var templates = Substitute.For<IEmailTemplateRenderer>();

        verifier.IssuePasswordResetCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("123456");
        templates.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
                 .Returns("<html>reset</html>");

        var job = new PasswordResetEmailJob(
            admin, verifier, sender, templates,
            Options.Create(new EmailVerificationOptions()),
            NullLogger<PasswordResetEmailJob>.Instance);

        return (job, admin, sender, verifier, templates);
    }

    [Fact]
    public async Task SendAsync_WhenNoAccountMatches_IssuesNoCodeAndSendsNothing()
    {
        var (job, admin, sender, verifier, _) = CreateSut();
        admin.FindUserByEmailAsync("ghost@example.com", Arg.Any<CancellationToken>())
             .Returns((KeycloakUserSummary?)null);

        await job.SendAsync("ghost@example.com", CancellationToken.None);

        await verifier.DidNotReceive()
            .IssuePasswordResetCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenAccountExists_IssuesCodeAndSendsHtmlEmail()
    {
        var (job, admin, sender, verifier, templates) = CreateSut();
        admin.FindUserByEmailAsync("owner@example.com", Arg.Any<CancellationToken>())
             .Returns(new KeycloakUserSummary("kc-1", Array.Empty<string>()));

        await job.SendAsync("owner@example.com", CancellationToken.None);

        await verifier.Received(1)
            .IssuePasswordResetCodeAsync("owner@example.com", Arg.Any<CancellationToken>());
        await templates.Received(1).RenderAsync(
            "PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            "owner@example.com",
            PasswordResetEmailJob.Subject,
            Arg.Any<string>(),
            "<html>reset</html>",
            Arg.Any<CancellationToken>());
    }
}
