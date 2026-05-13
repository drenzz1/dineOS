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
