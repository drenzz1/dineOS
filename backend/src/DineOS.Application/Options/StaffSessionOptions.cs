namespace DineOS.Application.Options;

/// <summary>
/// Configures the staff-session PIN flow (one business Keycloak account, many
/// PIN-gated staff identities). The business logs in via Keycloak; selecting a
/// staff member + PIN mints a short-lived, role-scoped token signed by this
/// backend (HS256 with <see cref="SigningKey"/>). The operational role lives in
/// that token, not in the Keycloak account — so the PIN is a real authorization
/// boundary, not a UI switch.
/// </summary>
public sealed class StaffSessionOptions
{
    public const string SectionName = "StaffSession";

    /// <summary>
    /// Symmetric HMAC-SHA256 signing key. Must be at least 32 bytes. MUST be
    /// overridden per environment (the appsettings value is a dev-only
    /// placeholder); leaking it lets an attacker mint any staff role.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token <c>iss</c> claim and the value the API validates against.</summary>
    public string Issuer { get; set; } = "dineos-staff-session";

    /// <summary>Token <c>aud</c> claim and the value the API validates against.</summary>
    public string Audience { get; set; } = "dineos-api";

    /// <summary>
    /// Staff-session lifetime in minutes. Defaults to a 12-hour shift; the
    /// session ends (or is switched) when the staff member signs out.
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 720;
}
