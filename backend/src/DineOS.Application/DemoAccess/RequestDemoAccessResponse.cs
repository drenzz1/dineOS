namespace DineOS.Application.DemoAccess;

/// <summary>
/// Always identical regardless of whether a row was created, reused, or the
/// honeypot was tripped — we don't leak existence of an account (#216).
/// </summary>
public sealed class RequestDemoAccessResponse
{
    public string Message { get; set; } =
        "If that email is eligible, we'll send the demo credentials shortly.";
}
