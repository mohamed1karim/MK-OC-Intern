using System.Security.Cryptography;

namespace db.Service.Auth;

// Hand-rolled PBKDF2 hasher using only System.Security.Cryptography (already
// part of the .NET runtime) — deliberately avoids pulling
// Microsoft.AspNetCore.Identity into this project just for its password
// hasher, since db.Service is a plain class library (Microsoft.NET.Sdk, not
// Sdk.Web) with no other ASP.NET Core package references.
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    // Distinctive prefix used to tell a real hash apart from one of the
    // legacy plaintext passwords seeded before this feature existed — no
    // plaintext password a user picked could plausibly start with this.
    private const string Prefix = "$PBKDF2$V1$";
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySizeBytes);

        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedValue)
    {
        if (!IsHashed(storedValue))
        {
            return false;
        }

        // Strip the prefix, then split the remaining "iterations$salt$hash".
        var parts = storedValue[Prefix.Length..].Split('$');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedKey = Convert.FromBase64String(parts[2]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    public bool IsHashed(string storedValue) => storedValue.StartsWith(Prefix, StringComparison.Ordinal);
}
