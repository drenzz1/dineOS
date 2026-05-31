namespace DineOS.Application.DTOs;

/// <summary>
/// Result of a successful PIN verification: a role-scoped staff-session access
/// token plus the identity it represents (for the client to display "who's
/// working" without decoding the token).
/// </summary>
public sealed record StaffSessionResponse(
    string AccessToken,
    int ExpiresIn,
    long StaffMemberId,
    string FullName,
    string Role);
