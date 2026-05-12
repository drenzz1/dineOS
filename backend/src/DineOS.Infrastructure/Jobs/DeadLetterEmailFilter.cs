using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// Hangfire state filter that captures permanently-failed
/// <see cref="OwnerInvitationEmailJob"/> executions into the
/// DeadLetterEmails table for visibility and manual replay.
///
/// FailedState is reached only after AutomaticRetry exhausts its budget,
/// so a row here represents a true permanent failure rather than a transient one.
/// </summary>
public sealed class DeadLetterEmailFilter(
    IServiceScopeFactory scopeFactory,
    ILogger<DeadLetterEmailFilter> logger)
    : JobFilterAttribute, IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failed)
            return;

        var jobType = context.BackgroundJob.Job?.Type;
        if (jobType is null || !typeof(IEmailJob).IsAssignableFrom(jobType))
            return;

        var args = context.BackgroundJob.Job?.Args;
        long? tenantId = args is { Count: > 0 } && args[0] is long id ? id : null;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entry = new DeadLetterEmail
            {
                ToAddress        = ResolveOwnerEmail(db, tenantId) ?? "(unknown)",
                Subject          = ResolveSubject(jobType),
                Body             = "(rendered at send time — see job logs by JobId)",
                JobId            = context.BackgroundJob.Id,
                JobType          = jobType.FullName ?? jobType.Name,
                AttemptCount     = ResolveRetryCount(context),
                FailureReason    = failed.Exception?.Message ?? "Unknown failure",
                ExceptionDetails = failed.Exception?.ToString(),
                FailedAt         = failed.FailedAt,
                TenantId         = tenantId,
            };

            db.DeadLetterEmails.Add(entry);
            db.SaveChanges();

            logger.LogError(
                "Email job dead-lettered: JobId={JobId} JobType={JobType} TenantId={TenantId} Reason={Reason}",
                entry.JobId, entry.JobType, entry.TenantId, entry.FailureReason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to write dead-letter row for JobId={JobId}",
                context.BackgroundJob.Id);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // No-op — we only react to entering FailedState.
    }

    private static string? ResolveOwnerEmail(AppDbContext db, long? tenantId)
    {
        if (tenantId is null) return null;
        return db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => t.OwnerEmail)
            .FirstOrDefault();
    }

    private static int ResolveRetryCount(ApplyStateContext context)
    {
        // AutomaticRetry stores attempt count in job parameters under "RetryCount".
        var raw = context.Connection.GetJobParameter(context.BackgroundJob.Id, "RetryCount");
        return int.TryParse(raw, out var count) ? count : 0;
    }

    private static string ResolveSubject(Type jobType)
    {
        // Each email job exposes a public const string Subject — reflect it so
        // the DLQ row carries a recognisable subject line.
        var field = jobType.GetField("Subject",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        return field?.GetValue(null) as string ?? "(unknown subject)";
    }
}
