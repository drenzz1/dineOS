namespace DineOS.Infrastructure.Auth;

/// <summary>
/// Centralized defaults + helpers for the firstName/lastName fields Keycloak's
/// declarative user profile expects on every user-create. Both
/// <c>OwnerProvisioningJob</c> and <c>DemoProvisioningJob</c> reach into this
/// type so the "single-word owner name produced an empty lastName, which
/// Keycloak rejects with 'Account is not fully set up'" class of bug cannot
/// silently re-appear in a sibling provisioning path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a placeholder is required.</b> The dineOS realm export sets
/// <c>"unmanagedAttributePolicy": "ENABLED"</c> on the declarative user
/// profile, which makes Keycloak run the declared-attribute validators on
/// every direct-grant token request. With the default profile config, both
/// <c>firstName</c> and <c>lastName</c> are declared and validated as
/// "non-empty after trim". A user whose <c>lastName</c> is the empty string
/// is treated as having an incomplete profile and the grant fails with
/// <c>"Account is not fully set up"</c> — the same error string used for
/// pending <c>requiredActions</c>. Verified end-to-end against the live
/// Docker stack (2026-05-22): setting <c>lastName</c> to a non-empty value
/// on a previously-stuck user immediately unlocks the direct-grant login.
/// </para>
/// <para>
/// <b>Why a sentinel and not a mirror of the first token.</b> Mirroring a
/// single-word name into both fields stores fabricated data — the user
/// never claimed their surname is the same as their first name. The
/// sentinel <see cref="MissingFieldPlaceholder"/> is deliberately a
/// non-letter glyph so it is visibly a placeholder in any UI that renders
/// the Keycloak user profile and so any downstream "display surname only"
/// path produces something obviously not a real name.
/// </para>
/// </remarks>
internal static class KeycloakProfileDefaults
{
    /// <summary>
    /// Sentinel written into <c>firstName</c>/<c>lastName</c> when the source
    /// signup payload did not provide the value. Em dash is used because it
    /// is a single visible glyph that is clearly not a name, won't be
    /// mistaken for real data, and passes Keycloak's "non-empty after trim"
    /// validator.
    /// </summary>
    public const string MissingFieldPlaceholder = "—";

    /// <summary>
    /// Splits a free-form display name into the <c>(firstName, lastName)</c>
    /// pair Keycloak expects, always returning non-empty values per the
    /// constraint documented on this type.
    /// </summary>
    /// <param name="displayName">
    /// Raw user-supplied name (e.g. signup form's <c>OwnerName</c>). May be
    /// null, empty, whitespace-only, or contain runs of whitespace between
    /// tokens — every shape is handled deterministically.
    /// </param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>null / whitespace-only → (placeholder, placeholder)</description></item>
    /// <item><description>single token (any surrounding whitespace) → (token, placeholder)</description></item>
    /// <item><description>two or more tokens → (first token, remaining tokens joined by single space)</description></item>
    /// </list>
    /// </returns>
    public static (string FirstName, string LastName) SplitDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return (MissingFieldPlaceholder, MissingFieldPlaceholder);

        // RemoveEmptyEntries + TrimEntries collapses runs of internal whitespace
        // ("Jane  Doe") and strips leading/trailing whitespace ("  test  ") in a
        // single pass, so downstream callers don't have to special-case either.
        var tokens = displayName.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            return (MissingFieldPlaceholder, MissingFieldPlaceholder);

        if (tokens.Length == 1)
            return (tokens[0], MissingFieldPlaceholder);

        return (tokens[0], string.Join(' ', tokens, 1, tokens.Length - 1));
    }
}
