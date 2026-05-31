namespace DineOS.Application.Authorization;

public static class Roles
{
    public const string SuperAdmin   = "SuperAdmin";

    /// <summary>
    /// Account-level role for the business's Keycloak account (#staff-pin-auth
    /// Phase 2): gates staff management + billing (<see cref="Policies.OwnerOnly"/>).
    /// <para>
    /// By design <c>Owner</c> is a composite over <see cref="Manager"/> in
    /// Keycloak, so the owner login also has full operational access. This is a
    /// deliberate, permanent choice (decided 2026-05-31): the owner/business
    /// account can do everything, and PIN-issued staff sessions exist for quick,
    /// role-scoped staff switching on a shared terminal (a Cashier session
    /// genuinely cannot perform Manager actions). We intentionally did NOT drop
    /// the composite to force owners to PIN-in for operations — that would
    /// degrade UX without adding security, since the staff-session boundary is
    /// already enforced. Do not "tighten" this without revisiting that call.
    /// </para>
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
