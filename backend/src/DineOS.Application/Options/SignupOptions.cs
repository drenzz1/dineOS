namespace DineOS.Application.Options;

/// <summary>
/// Configures the post-checkout owner provisioning flow (#205): the frontend
/// "first-login" URL embedded in the welcome email so the freshly-provisioned
/// owner lands in the dineOS app's password-rotation page (which signs them
/// straight into the app) rather than in Keycloak's account console.
/// </summary>
public sealed class SignupOptions
{
    public const string SectionName = "Signup";

    /// <summary>
    /// "Set your password" link embedded in the owner welcome email. The
    /// owner's email is appended as a <c>?email=…</c> query string so the
    /// first-login form is pre-filled. Required — there is no safe default
    /// because a hardcoded <c>http://localhost</c> fallback would silently
    /// ship to non-dev environments.
    /// </summary>
    public string FirstLoginUrl { get; set; } = string.Empty;
}
