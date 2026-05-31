namespace DineOS.Application.Authorization;

public static class Roles
{
    public const string SuperAdmin   = "SuperAdmin";
    public const string Manager      = "Manager";
    public const string Cashier      = "Cashier";
    public const string KitchenStaff = "KitchenStaff";
}

public static class Policies
{
    public const string SuperAdminOnly   = "SuperAdminOnly";
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
