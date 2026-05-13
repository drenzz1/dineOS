using DineOS.Application.DTOs;
using DineOS.Application.Restaurants;

namespace DineOS.Tests.Unit.Validators;

public class AuthRequestValidatorsTests
{
    [Fact]
    public void LoginRequestValidator_Accepts_NonEmptyCredentials()
    {
        var v = new LoginRequestValidator();
        var result = v.Validate(new LoginRequest("admin@dineos.dev", "Test1234!"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LoginRequestValidator_Rejects_EmptyUsernameAndPassword()
    {
        var v = new LoginRequestValidator();
        var result = v.Validate(new LoginRequest(string.Empty, string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username is required."));
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password is required."));
    }

    [Fact]
    public void LoginRequestValidator_Rejects_OverlongUsername()
    {
        var v = new LoginRequestValidator();
        var result = v.Validate(new LoginRequest(new string('a', 101), "Test1234!"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Username");
    }

    [Fact]
    public void RefreshTokenRequestValidator_Accepts_NonEmptyToken()
    {
        var v = new RefreshTokenRequestValidator();
        var result = v.Validate(new RefreshTokenRequest("eyJ.payload.sig"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RefreshTokenRequestValidator_Rejects_EmptyToken()
    {
        var v = new RefreshTokenRequestValidator();
        var result = v.Validate(new RefreshTokenRequest(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Refresh token is required."));
    }

    [Fact]
    public void LogoutRequestValidator_Accepts_NonEmptyToken()
    {
        var v = new LogoutRequestValidator();
        var result = v.Validate(new LogoutRequest("eyJ.payload.sig"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void LogoutRequestValidator_Rejects_EmptyToken()
    {
        var v = new LogoutRequestValidator();
        var result = v.Validate(new LogoutRequest(string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Refresh token is required."));
    }

    [Fact]
    public void ConfirmEmailVerificationRequestValidator_Accepts_SixDigitCode()
    {
        var v = new ConfirmEmailVerificationRequestValidator();
        var result = v.Validate(new ConfirmEmailVerificationRequest { Code = "123456" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ConfirmEmailVerificationRequestValidator_Rejects_EmptyCode()
    {
        var v = new ConfirmEmailVerificationRequestValidator();
        var result = v.Validate(new ConfirmEmailVerificationRequest { Code = string.Empty });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("required"));
    }

    [Theory]
    [InlineData("12345")]    // too short
    [InlineData("1234567")]  // too long
    [InlineData("abcdef")]   // non-digit
    [InlineData("12 456")]   // whitespace
    public void ConfirmEmailVerificationRequestValidator_Rejects_NonSixDigitCode(string code)
    {
        var v = new ConfirmEmailVerificationRequestValidator();
        var result = v.Validate(new ConfirmEmailVerificationRequest { Code = code });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("6 digits"));
    }
}
