using System.Security.Cryptography;

namespace PortalProveedores.Application.Security;

public interface ICryptoService
{
    (string Hash, string Salt) HashPassword(string password);
    bool VerifyPassword(string password, string hash, string salt);
}

public class CryptoService : ICryptoService
{
    private const int SaltSize = 32; // 256 bits
    private const int KeySize = 64;  // 512 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            Algorithm,
            KeySize
        );

        return (
            Convert.ToBase64String(hashBytes),
            Convert.ToBase64String(saltBytes)
        );
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            return false;

        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] expectedHashBytes = Convert.FromBase64String(hash);

        byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            Algorithm,
            KeySize
        );

        // Comparación en tiempo constante para prevenir ataques de temporización (Timing Attacks)
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }
}
