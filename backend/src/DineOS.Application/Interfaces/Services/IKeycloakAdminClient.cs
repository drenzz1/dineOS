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
    /// </summary>
    Task<string> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string tempPassword,
        IReadOnlyList<string> requiredActions,
        CancellationToken ct);

    /// <summary>
    /// Assigns a realm role to the given user. If the role does not exist
    /// in the realm yet, it is created (handles dev volumes where the
    /// realm-export was imported before the role was defined).
    /// </summary>
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken ct);
}
