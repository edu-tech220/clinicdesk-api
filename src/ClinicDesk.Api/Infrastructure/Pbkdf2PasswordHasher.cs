using System.Security.Cryptography;
using ClinicDesk.Api.Application.Abstractions;

namespace ClinicDesk.Api.Infrastructure;

/// <summary>Hashing de contrasenas con PBKDF2-SHA256, sin dependencias externas (ASP.NET Identity no se usa aqui).</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public (string Hash, string Salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, KeySizeBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, Algorithm, KeySizeBytes);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
