namespace DineOS.Domain.Enums;

/// <summary>
/// Lifecycle status for a demo access user (#216).
/// </summary>
public enum DemoUserStatus
{
    /// <summary>Row created; Keycloak provisioning has not run yet.</summary>
    Pending = 0,

    /// <summary>Keycloak user exists, role assigned, demo is usable.</summary>
    Active = 1,

    /// <summary>Past <c>ExpiresAt</c>; Keycloak user has been disabled by the cleanup job.</summary>
    Expired = 2,

    /// <summary>Manually disabled (e.g. abuse).</summary>
    Disabled = 3,
}
