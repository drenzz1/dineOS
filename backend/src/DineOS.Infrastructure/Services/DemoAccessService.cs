using DineOS.Application.Common;
using DineOS.Application.DemoAccess;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Auth;
using DineOS.Infrastructure.Jobs;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

/// <summary>
/// Demo access flow (#216). Three idempotency branches:
///   <list type="bullet">
///     <item>new email → insert <c>DemoUser(Pending)</c>, enqueue <see cref="DemoProvisioningJob"/></item>
///     <item>Active &amp; not expired → enqueue <see cref="DemoCredentialsResendJob"/> (rotates KC password)</item>
///     <item>Expired → reset <c>ExpiresAt</c>, mark <c>Pending</c>, re-enqueue provisioning</item>
///   </list>
/// Honeypot trip → constant response, no row, no job. The response itself is
/// constant so the API does not reveal account existence.
/// </summary>
public sealed class DemoAccessService(
    AppDbContext db,
    IValidator<RequestDemoAccessRequest> validator,
    IBackgroundJobClient backgroundJobs,
    IOptions<DemoOptions> demoOptions,
    ILogger<DemoAccessService> logger) : IDemoAccessService
{
    public async Task<ServiceResult<RequestDemoAccessResponse>> RequestAsync(
        RequestDemoAccessRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var opts = demoOptions.Value;
        if (!opts.Enabled)
        {
            return ServiceResult<RequestDemoAccessResponse>.NotFound("Demo access is not available.");
        }

        // Honeypot tripped → silently succeed with no side effects. Same response
        // shape and timing as the happy path so bots can't probe the field.
        if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            logger.LogInformation(
                "Demo access honeypot tripped. Ip={IpAddress}", ipAddress);
            return ServiceResult<RequestDemoAccessResponse>.Ok(new RequestDemoAccessResponse());
        }

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<RequestDemoAccessResponse>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var expiresAt = now + opts.AccountTtl;

        var existing = await db.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Email == normalizedEmail, ct);

        if (existing is null)
        {
            var tempPassword = TempPasswordGenerator.Generate();

            var demoUser = new DemoUser
            {
                Email       = normalizedEmail,
                RequestedAt = now,
                ExpiresAt   = expiresAt,
                IpAddress   = ipAddress,
                Status      = DemoUserStatus.Pending,
            };
            db.DemoUsers.Add(demoUser);
            await db.SaveChangesAsync(ct);

            backgroundJobs.Enqueue<DemoProvisioningJob>(
                job => job.RunAsync(demoUser.Id, tempPassword, CancellationToken.None));

            logger.LogInformation(
                "Demo access requested (new). DemoUserId={DemoUserId} Email={Email}",
                demoUser.Id, normalizedEmail);
        }
        else if (existing.Status == DemoUserStatus.Active && existing.ExpiresAt > now)
        {
            var tempPassword = TempPasswordGenerator.Generate();
            existing.IpAddress = ipAddress;
            await db.SaveChangesAsync(ct);

            backgroundJobs.Enqueue<DemoCredentialsResendJob>(
                job => job.RunAsync(existing.Id, tempPassword, CancellationToken.None));

            logger.LogInformation(
                "Demo access re-requested (active reuse). DemoUserId={DemoUserId} Email={Email}",
                existing.Id, normalizedEmail);
        }
        else
        {
            // Expired or Disabled or stuck in Pending — reset and re-provision.
            var tempPassword = TempPasswordGenerator.Generate();
            existing.RequestedAt = now;
            existing.ExpiresAt   = expiresAt;
            existing.IpAddress   = ipAddress;
            existing.Status      = DemoUserStatus.Pending;
            await db.SaveChangesAsync(ct);

            backgroundJobs.Enqueue<DemoProvisioningJob>(
                job => job.RunAsync(existing.Id, tempPassword, CancellationToken.None));

            logger.LogInformation(
                "Demo access re-requested (expired/reset). DemoUserId={DemoUserId} Email={Email}",
                existing.Id, normalizedEmail);
        }

        return ServiceResult<RequestDemoAccessResponse>.Ok(new RequestDemoAccessResponse());
    }
}
