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
    /// Tenant owners (#205) pass <c>temporaryPassword: true</c> with
    /// <c>requiredActions: ["UPDATE_PASSWORD"]</c> so the FE routes them
    /// through the dedicated first-login password-change flow; demo users
    /// (#216) pass <c>temporaryPassword: false</c> with no required actions
    /// so the emailed creds ARE the credential (direct-grant login from the
    /// frontend would otherwise fail with <c>resolve_required_actions</c>).
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

    /// <summary>
    /// Returns the user's id and currently pending <c>requiredActions</c>
    /// list, or <c>null</c> if no user matches the email. Used by the
    /// first-login password-change flow to verify the caller is in the
    /// expected post-provisioning state before honouring the password change.
    /// </summary>
    Task<KeycloakUserSummary?> FindUserByEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Replaces the user's <c>requiredActions</c> list. Pass an empty list
    /// to clear all pending actions so direct-access grant logins succeed.
    /// </summary>
    Task SetRequiredActionsAsync(string userId, IReadOnlyList<string> requiredActions, CancellationToken ct);

    /// <summary>
    /// Sets the <c>emailVerified</c> flag on a Keycloak user. Called when the
    /// owner completes the first-login password change — which is only possible
    /// if they received the emailed temporary credentials — so the IdP reflects
    /// the now-proven verified state.
    /// </summary>
    Task SetEmailVerifiedAsync(string userId, bool emailVerified, CancellationToken ct);
}

/// <summary>
/// Minimal Keycloak user representation returned by lookup endpoints.
/// </summary>
public sealed record KeycloakUserSummary(string Id, IReadOnlyList<string> RequiredActions);
