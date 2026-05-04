using DineOS.Application.Interfaces.Services;

namespace DineOS.Infrastructure.Services;

public class PinHasher : IPinHasher
{
    public string Hash(string pin) => BCrypt.Net.BCrypt.HashPassword(pin);
    public bool Verify(string pin, string hash) => BCrypt.Net.BCrypt.Verify(pin, hash);
}
