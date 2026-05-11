namespace DineOS.Application.DTOs;

public record PlatformUserDto(
    long Id,
    long TenantId,
    string TenantName,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt);
