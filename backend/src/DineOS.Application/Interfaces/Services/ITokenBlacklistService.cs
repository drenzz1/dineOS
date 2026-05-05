namespace DineOS.Application.Interfaces.Services;

public interface ITokenBlacklistService
{
    Task BlacklistAsync(string jti, TimeSpan ttl);
    Task<bool> IsBlacklistedAsync(string jti);
}
