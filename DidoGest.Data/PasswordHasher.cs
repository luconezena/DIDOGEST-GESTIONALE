using System;
using System.Security.Cryptography;

namespace DidoGest.Data.Services;

public static class PasswordHasher
{
    // Parametri ragionevoli per un'app desktop; si possono aumentare se serve.
    private const int SaltSize = 16; // 128-bit
    private const int KeySize = 32;  // 256-bit
    private const int Iterations = 100_000;

    public static (byte[] hash, byte[] salt) HashPassword(string password)
    {
        if (password is null)
            throw new ArgumentNullException(nameof(password));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La password non può essere vuota.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return (hash, salt);
    }

    public static bool Verify(string password, byte[] expectedHash, byte[] salt)
    {
        if (password is null)
            throw new ArgumentNullException(nameof(password));
        if (expectedHash is null)
            throw new ArgumentNullException(nameof(expectedHash));
        if (salt is null)
            throw new ArgumentNullException(nameof(salt));

        if (expectedHash.Length == 0 || salt.Length == 0)
            return false;

        var computed = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(computed, expectedHash);
    }
}
