namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Server-to-server Keycloak Admin REST API client. Used to provision the
/// tenant owner's account (and assign the Owner realm role) after a Stripe
/// checkout completes (#205). Idempotent at the API level: 409 on user
/// create is recovered by looking up the existing user by email.
/// </summary>
public interface IKeycloakAdminClient
{
    /// <summary>
    /// Creates a Keycloak user in the configured realm and returns the
    /// Keycloak user id. If a user with the same email already exists,
    /// returns that user's id instead of failing.
    /// Pass <c>temporaryPassword: true</c> for tenant owners (Keycloak adds
    /// <c>UPDATE_PASSWORD</c> automatically) and <c>false</c> for demo users
    /// (direct-grant login from the frontend fails with
    /// <c>resolve_required_actions</c> otherwise).
    /// </summary>
    Task<string> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string tempPassword,
        IReadOnlyList<string> requiredActions,
        bool temporaryPassword,
        CancellationToken ct);

    /// <summary>
    /// Assigns a realm role to the given user. If the role does not exist
    /// in the realm yet, it is created (handles dev volumes where the
    /// realm-export was imported before the role was defined).
    /// </summary>
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken ct);

    /// <summary>
    /// Replaces the given user's password. Used by the demo-access flow (#216)
    /// when a visitor re-requests credentials within the TTL — the old password
    /// is invalidated and a fresh one is emailed.
    /// </summary>
    Task ResetPasswordAsync(string userId, string newPassword, bool temporary, CancellationToken ct);

    /// <summary>
    /// Sets the <c>enabled</c> flag on a Keycloak user. Used by the demo
    /// cleanup job to disable expired demo users (#216).
    /// </summary>
    Task SetUserEnabledAsync(string userId, bool enabled, CancellationToken ct);

    /// <summary>
    /// Sets one custom user attribute on a Keycloak user. Used by the
    /// demo-access flow to stamp the demo tenant id so the JWT carries the
    /// <c>tenant_id</c> claim the API tenancy filter expects.
    /// </summary>
    Task SetUserAttributeAsync(string userId, string attributeName, string value, CancellationToken ct);
}
