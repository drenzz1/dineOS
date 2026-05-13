using DineOS.Application.Authorization;
using System.Text.RegularExpressions;

namespace DineOS.Tests.Unit;

/// <summary>
/// Build-time regression guard: fails dotnet test if any [Authorize] attribute in any
/// source file still uses a raw string literal for a role or policy name.
/// Use Roles.* and Policies.* constants from DineOS.Application.Authorization instead.
/// </summary>
public class NoMagicRoleStringTests
{
    // Patterns that must NOT appear inside an [Authorize(...)] attribute
    private static readonly string[] BannedPolicyPatterns =
    [
        $"Policy = \"{Policies.SuperAdminOnly}\"",
        $"Policy = \"{Policies.ManagerAndAbove}\"",
        $"Policy = \"{Policies.CashierAndAbove}\"",
        $"Policy = \"{Policies.KitchenStaffOnly}\""
    ];

    private static readonly string[] BannedRolePatterns =
    [
        $"Roles = \"{Roles.SuperAdmin}\"",
        $"Roles = \"{Roles.Manager}\"",
        $"Roles = \"{Roles.Cashier}\"",
        $"Roles = \"{Roles.KitchenStaff}\""
    ];

    // Captures a complete [Authorize(...)] attribute (handles multi-property forms)
    private static readonly Regex AuthorizeAttrRegex = new(
        @"\[Authorize\s*\([^\]]*\)\]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void NoAuthorizeAttribute_UsesMagicRoleOrPolicyString()
    {
        var backendRoot = FindBackendRoot();
        var violations  = new List<string>();

        var sourceFiles = Directory
            .EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("AuthorizationConstants.cs", StringComparison.OrdinalIgnoreCase));

        foreach (var file in sourceFiles)
        {
            var text      = File.ReadAllText(file);
            var shortPath = Path.GetRelativePath(backendRoot, file);

            // Replace comment lines with blank space so line numbers stay correct
            // but doc-comment references to policy names don't trigger false positives.
            var code = Regex.Replace(text, @"^\s*//.*$", m => new string(' ', m.Length),
                RegexOptions.Multiline);

            foreach (Match match in AuthorizeAttrRegex.Matches(code))
            {
                var attr = match.Value;
                var line = code[..match.Index].Count(c => c == '\n') + 1;

                foreach (var pattern in BannedPolicyPatterns)
                {
                    if (attr.Contains(pattern))
                        violations.Add($"{shortPath}:{line}  →  {attr.Trim()}");
                }

                foreach (var pattern in BannedRolePatterns)
                {
                    if (attr.Contains(pattern))
                        violations.Add($"{shortPath}:{line}  →  {attr.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Magic role/policy string literals detected in [Authorize] attributes.\n" +
            "Use Roles.* and Policies.* constants from DineOS.Application.Authorization:\n\n" +
            string.Join("\n", violations));
    }

    // Walk up from the test output directory until we find the backend root
    // (the directory that has both 'src' and 'tests' subdirectories).
    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetDirectories("src").Length > 0 &&
                dir.GetDirectories("tests").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Cannot locate the backend root (expected a directory with both 'src' and 'tests' subdirectories). " +
            $"AppContext.BaseDirectory = {AppContext.BaseDirectory}");
    }
}
