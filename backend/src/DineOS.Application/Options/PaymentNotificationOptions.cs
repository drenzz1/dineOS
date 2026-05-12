namespace DineOS.Application.Options;

public sealed class PaymentNotificationOptions
{
    public const string SectionName = "PaymentNotifications";

    /// <summary>
    /// A pending payment older than this is considered overdue.
    /// </summary>
    public int OverdueThresholdMinutes { get; init; } = 30;

    /// <summary>
    /// Cron expression for the daily summary recurring job.
    /// Default: 23:55 every day, restaurant local time is server time.
    /// </summary>
    public string DailySummaryCron { get; init; } = "55 23 * * *";

    /// <summary>
    /// Cron expression for the overdue payment scan recurring job.
    /// Default: every 5 minutes.
    /// </summary>
    public string OverdueScanCron { get; init; } = "*/5 * * * *";
}
