using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DineOS.Tests.Unit;

/// <summary>
/// Roslyn-based build guard: parses every C# source file into a full syntax tree and
/// fails if any [Authorize] attribute argument is a raw string literal whose value
/// matches one of the eight reserved role or policy names.
///
/// Unlike the regex-based NoMagicRoleStringTests, this test operates on AST nodes,
/// so comments, doc strings, and embedded JSON are naturally excluded — only genuine
/// attribute argument expressions are inspected.
/// </summary>
public class NoMagicStringRoslynTests
{
    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Manager")]
    [InlineData("Cashier")]
    [InlineData("KitchenStaff")]
    [InlineData("SuperAdminOnly")]
    [InlineData("ManagerAndAbove")]
    [InlineData("CashierAndAbove")]
    [InlineData("KitchenStaffOnly")]
    public void NoAuthorizeAttribute_ArgumentIsStringLiteral(string forbidden)
    {
        var backendRoot = FindBackendRoot();
        var violations  = new List<string>();

        var sourceFiles = Directory
            .EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var file in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var root = tree.GetRoot();

            foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                // Match [Authorize] and [AuthorizeAttribute], qualified or unqualified
                var simpleName = attr.Name.ToString().Split('.').Last();
                if (simpleName is not ("Authorize" or "AuthorizeAttribute"))
                    continue;

                if (attr.ArgumentList is null)
                    continue;

                foreach (var arg in attr.ArgumentList.Arguments)
                {
                    if (arg.Expression is not LiteralExpressionSyntax lit)
                        continue;
                    if (!lit.IsKind(SyntaxKind.StringLiteralExpression))
                        continue;
                    if (lit.Token.ValueText != forbidden)
                        continue;

                    var line      = tree.GetLineSpan(lit.Span).StartLinePosition.Line + 1;
                    var shortPath = Path.GetRelativePath(backendRoot, file);
                    violations.Add($"{shortPath}:{line}  →  {attr}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Raw string literal \"{forbidden}\" found inside [Authorize] attribute — " +
            $"use Roles.* or Policies.* constants from DineOS.Application.Authorization:\n\n" +
            string.Join("\n", violations));
    }

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
