namespace DineOS.Application.Authorization;

public static class Roles
{
    public const string SuperAdmin   = "SuperAdmin";

    /// <summary>
    /// Account-level role for the business's Keycloak account (#staff-pin-auth
    /// Phase 2): staff management, billing, settings. Distinct from the
    /// operational roles below, which a staff member acquires per-shift via a
    /// PIN-issued staff session. During the transition <c>Owner</c> is a
    /// composite over <see cref="Manager"/> in Keycloak, so an owner token also
    /// carries operational access (and the FE's getPrimaryRole still resolves
    /// to Manager). The final tightening drops that composite once the staff
    /// roster/PIN UI ships.
    /// </summary>
    public const string Owner        = "Owner";

    public const string Manager      = "Manager";
    public const string Cashier      = "Cashier";
    public const string KitchenStaff = "KitchenStaff";
}

public static class Policies
{
    public const string SuperAdminOnly   = "SuperAdminOnly";

    /// <summary>
    /// Account-level capabilities (staff management, billing). Requires the
    /// business <see cref="Roles.Owner"/> account — a staff-session role
    /// (Manager/Cashier/KitchenStaff) does NOT satisfy it, so a PIN-selected
    /// staff member cannot manage staff or billing.
    /// </summary>
    public const string OwnerOnly        = "OwnerOnly";

    public const string ManagerAndAbove  = "ManagerAndAbove";
    public const string CashierAndAbove  = "CashierAndAbove";
    public const string KitchenStaffOnly = "KitchenStaffOnly";
}

public static class AuthSchemes
{
    /// <summary>
    /// JwtBearer scheme for backend-issued, PIN-gated staff-session tokens
    /// (one business Keycloak account, many role-scoped staff identities).
    /// Registered alongside the default Keycloak Bearer scheme; operational
    /// authorization policies accept either.
    /// </summary>
    public const string StaffSession = "StaffSession";

    /// <summary>The Keycloak-backed default Bearer scheme name (JwtBearerDefaults.AuthenticationScheme).</summary>
    public const string Keycloak = "Bearer";
}
