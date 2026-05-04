namespace DineOS.Application.Interfaces.Services;

public interface IPinHasher
{
    string Hash(string pin);
    bool Verify(string pin, string hash);
}
