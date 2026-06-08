using DineOS.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace DineOS.Tests.Authorization;

/// <summary>
/// Runtime reflection guard: loads the DineOS.Api assembly, enumerates every
/// [Authorize] attribute on every type and member, and asserts that all Policy
/// and Roles values belong to the known Policies.* / Roles.* constant sets.
///
/// Note: C# const strings compile to the same IL as their literal values, so
/// reflection cannot distinguish a constant reference from a raw string literal.
/// Source-literal detection is handled by the Roslyn-based guards in Unit/.
/// This test provides a complementary runtime whitelist check: any Policy or
/// Roles value that is NOT in the known set (e.g. a typo or an undeclared name)
/// will fail here even if it compiles cleanly.
/// </summary>
public class NoMagicStringsTests
{
    private static readonly HashSet<string> KnownPolicies = new(StringComparer.Ordinal)
    {
        Policies.SuperAdminOnly,
        Policies.BusinessAccountOnly,
        Policies.OwnerOnly,
        Policies.ManagerAndAbove,
        Policies.CashierAndAbove,
        Policies.KitchenAccess,
        Policies.KitchenStaffOnly,
    };

    private static readonly HashSet<string> KnownRoles = new(StringComparer.Ordinal)
    {
        Roles.SuperAdmin,
        Roles.Owner,
        Roles.Manager,
        Roles.Cashier,
        Roles.KitchenStaff,
    };

    [Fact]
    public void AllAuthorizeAttributes_PolicyAndRoles_AreFromKnownConstantSets()
    {
        // typeof(Program) references DineOS.Api's generated top-level-statement class,
        // which is the same anchor used by CustomWebApplicationFactory<Program>.
        var apiAssembly = typeof(Program).Assembly;
        var violations  = new List<string>();

        foreach (var type in apiAssembly.GetTypes())
        {
            Inspect(type.GetCustomAttributes<AuthorizeAttribute>(inherit: false),
                    type.FullName ?? type.Name,
                    violations);

            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.DeclaredOnly))
            {
                Inspect(member.GetCustomAttributes<AuthorizeAttribute>(inherit: false),
                        $"{type.FullName}.{member.Name}",
                        violations);
            }
        }

        Assert.True(violations.Count == 0,
            "Found [Authorize] attributes whose Policy or Roles value is not in the " +
            "known constant sets. Use Policies.* and Roles.* from " +
            "DineOS.Application.Authorization:\n\n" +
            string.Join("\n", violations));
    }

    private static void Inspect(
        IEnumerable<AuthorizeAttribute> attrs,
        string location,
        List<string> violations)
    {
        foreach (var attr in attrs)
        {
            if (attr.Policy is { } policy && !KnownPolicies.Contains(policy))
                violations.Add($"  {location}: Policy = \"{policy}\"");

            if (attr.Roles is { } roles)
            {
                foreach (var role in roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!KnownRoles.Contains(role))
                        violations.Add($"  {location}: Roles = \"{role}\"");
                }
            }
        }
    }
}
