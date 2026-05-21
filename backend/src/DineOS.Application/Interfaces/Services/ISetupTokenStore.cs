namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// One-time setup tokens emailed to newly-provisioned tenant owners so they
/// can land on the dineOS <c>/set-password</c> page without ever seeing the
/// Keycloak login UI. Backed by Redis with a TTL — once consumed the token
/// is deleted so a stolen email link can't be replayed.
/// </summary>
public interface ISetupTokenStore
{
    /// <summary>
    /// Generates and stores a cryptographically random token (32 bytes,
    /// base64url-encoded) mapped to <paramref name="tenantId"/>. Returns the
    /// raw token for embedding in the email link.
    /// </summary>
    Task<string> IssueAsync(long tenantId, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Looks up the tenant id for a token without deleting it. Returns null
    /// if the token is unknown or expired.
    /// </summary>
    Task<long?> PeekAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Atomically returns the tenant id for a token and deletes it. Returns
    /// null if the token was already used, unknown, or expired.
    /// </summary>
    Task<long?> ConsumeAsync(string token, CancellationToken ct = default);
}
