namespace DineOS.Application.DTOs;

public record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
