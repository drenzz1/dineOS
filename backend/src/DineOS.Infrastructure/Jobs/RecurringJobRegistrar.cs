using DineOS.Application.Options;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Jobs;

/// <summary>
/// IHostedService that registers recurring Hangfire jobs at startup.
/// </summary>
public sealed class RecurringJobRegistrar(
    IRecurringJobManager recurring,
    IOptions<PaymentNotificationOptions> options,
    IOptions<DemoOptions> demoOptions,
    ILogger<RecurringJobRegistrar> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        recurring.AddOrUpdate<DailyPaymentSummaryJob>(
            DailyPaymentSummaryJob.RecurringJobId,
            job => job.RunAsync(CancellationToken.None),
            opts.DailySummaryCron);

        recurring.AddOrUpdate<OverduePaymentNotificationJob>(
            OverduePaymentNotificationJob.RecurringJobId,
            job => job.RunAsync(CancellationToken.None),
            opts.OverdueScanCron);

        var demo = demoOptions.Value;
        if (demo.Enabled)
        {
            recurring.AddOrUpdate<DemoCleanupJob>(
                DemoCleanupJob.RecurringJobId,
                job => job.RunAsync(CancellationToken.None),
                demo.CleanupCron);
        }
        else
        {
            recurring.RemoveIfExists(DemoCleanupJob.RecurringJobId);
        }

        logger.LogInformation(
            "Recurring jobs registered: DailySummary={DailyCron} OverdueScan={OverdueCron} DemoCleanup={DemoCron} DemoEnabled={DemoEnabled}",
            opts.DailySummaryCron, opts.OverdueScanCron, demo.CleanupCron, demo.Enabled);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
