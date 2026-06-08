using DineOS.Infrastructure.Auth;

namespace DineOS.Tests.Unit;

/// <summary>
/// Locks in the contract documented on <see cref="KeycloakProfileDefaults"/>:
/// every shape of free-form display name maps to a <c>(firstName, lastName)</c>
/// pair where both fields are non-empty, so Keycloak's declarative
/// user-profile validator never rejects a direct-grant login with
/// "Account is not fully set up". The shared helper is the single place
/// both <c>OwnerProvisioningJob</c> and <c>DemoProvisioningJob</c> reach
/// for these defaults, so a regression here is a regression in both flows.
/// </summary>
public class KeycloakProfileDefaultsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\t\n  ")]
    public void SplitDisplayName_WhitespaceOnlyInput_ReturnsPlaceholderForBothFields(
        string? input)
    {
        var (first, last) = KeycloakProfileDefaults.SplitDisplayName(input);

        Assert.Equal(KeycloakProfileDefaults.MissingFieldPlaceholder, first);
        Assert.Equal(KeycloakProfileDefaults.MissingFieldPlaceholder, last);
    }

    [Theory]
    [InlineData("test",     "test")]
    [InlineData("  test  ", "test")]
    [InlineData("\tDren\t", "Dren")]
    [InlineData("Jane  ",   "Jane")]
    public void SplitDisplayName_SingleToken_ReturnsTokenAndPlaceholder(
        string input, string expectedFirst)
    {
        // Reviewer note (2026-05-22): we explicitly do NOT mirror the first
        // token into lastName for single-word names — that would store
        // fabricated surname data. A visible placeholder is the honest
        // representation of "lastName was not provided".
        var (first, last) = KeycloakProfileDefaults.SplitDisplayName(input);

        Assert.Equal(expectedFirst, first);
        Assert.Equal(KeycloakProfileDefaults.MissingFieldPlaceholder, last);
    }

    [Theory]
    [InlineData("Jane Doe",          "Jane", "Doe")]
    [InlineData("Jane  Doe",         "Jane", "Doe")]
    [InlineData("Jane\tDoe",         "Jane", "Doe")]
    [InlineData("  Jane  Doe  ",     "Jane", "Doe")]
    [InlineData("Mary Anne Smith",   "Mary", "Anne Smith")]
    [InlineData("José  de la Cruz",  "José", "de la Cruz")]
    [InlineData("a b c d e",         "a",    "b c d e")]
    public void SplitDisplayName_MultipleTokens_FirstTokenIsFirstNameRestJoined(
        string input, string expectedFirst, string expectedLast)
    {
        var (first, last) = KeycloakProfileDefaults.SplitDisplayName(input);

        Assert.Equal(expectedFirst, first);
        Assert.Equal(expectedLast, last);
    }

    [Fact]
    public void SplitDisplayName_AlwaysReturnsNonEmptyValuesForAnyInput()
    {
        // Property-style sweep over inputs that have historically tripped
        // Keycloak's "Account is not fully set up" rejection.
        string?[] inputs =
        [
            null, "", " ", "   ", "\t", "\n",
            "test", "  test  ", "Jane Doe", "Jane  Doe",
            "Mary Anne Smith", "a", "a b", "a b c",
        ];

        foreach (var input in inputs)
        {
            var (first, last) = KeycloakProfileDefaults.SplitDisplayName(input);
            Assert.False(string.IsNullOrWhiteSpace(first),
                $"firstName must be non-empty for input '{input}'.");
            Assert.False(string.IsNullOrWhiteSpace(last),
                $"lastName must be non-empty for input '{input}'.");
        }
    }
}
