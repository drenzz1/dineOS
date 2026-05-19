using System.Security.Cryptography;

namespace DineOS.Infrastructure.Auth;

/// <summary>
/// Generates short-lived, single-use temporary passwords for newly
/// provisioned Keycloak owner accounts (#205). The output is shuffled
/// after seeding one character from each policy class (upper, lower,
/// digit, symbol) so it satisfies Keycloak's default password policy.
/// </summary>
internal static class TempPasswordGenerator
{
    private const string Upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower   = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits  = "23456789";
    private const string Symbols = "!@#$%&*?";
    private const string All     = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = 12)
    {
        if (length < 8)
            throw new ArgumentOutOfRangeException(nameof(length), "Temp password must be at least 8 characters.");

        var chars = new char[length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (int i = 4; i < length; i++)
            chars[i] = Pick(All);

        Shuffle(chars);
        return new string(chars);
    }

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(0, alphabet.Length)];

    private static void Shuffle(char[] buffer)
    {
        for (int i = buffer.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }
    }
}
