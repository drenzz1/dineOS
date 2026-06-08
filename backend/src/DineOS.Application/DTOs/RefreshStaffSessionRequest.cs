namespace DineOS.Application.DTOs;

/// <summary>Exchanges a staff refresh token for a fresh access token (no re-PIN).</summary>
public sealed class RefreshStaffSessionRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>Ends a staff session (server-side revocation of the refresh token).</summary>
public sealed class EndStaffSessionRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
