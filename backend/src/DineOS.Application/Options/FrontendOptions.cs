namespace DineOS.Application.Options;

/// <summary>
/// Public-facing dineOS web app base URL. Used by backend jobs (e.g. the
/// owner welcome email) to build links the user clicks from external
/// channels (email, SMS) and lands on the Next.js app.
/// </summary>
public class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>
    /// Origin (scheme + host[:port]) of the dineOS frontend. No trailing slash.
    /// Defaults to local dev. Override via <c>Frontend__BaseUrl</c>.
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:3000";
}
