namespace DineOS.Application.Options;

/// <summary>
/// Configures the demo-access flow (#216): feature flag, target tenant, TTL,
/// and the login URL embedded in welcome emails.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>When false, <c>POST /api/v1/demo/request</c> returns 404.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Slug of the seeded shared demo tenant.</summary>
    public string TenantSlug { get; set; } = "demo";

    /// <summary>Account lifetime in days; default 7.</summary>
    public int AccountTtlDays { get; set; } = 7;

    /// <summary>"Sign in" link embedded in the welcome email.</summary>
    public string LoginUrl { get; set; } = "http://localhost:3000/login";

    /// <summary>Realm role granted to provisioned demo users.</summary>
    public string RealmRole { get; set; } = "Demo";

    /// <summary>Daily cleanup cron expression (server local time).</summary>
    public string CleanupCron { get; set; } = "0 3 * * *";

    public TimeSpan AccountTtl => TimeSpan.FromDays(Math.Max(1, AccountTtlDays));
}
