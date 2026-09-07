using System.Security.Cryptography;

namespace PortalProveedores.Application.Security;

public interface ICryptoService
{
    string HashPassword(string password);
    string HashPasswordCombined(string password);
    (string Hash, string Salt) HashPasswordWithSalt(string password);
    bool VerifyPassword(string password, string storedHash);
    bool VerifyPassword(string password, string hash, string salt);
}

public class CryptoService : ICryptoService
{
    private const int SaltSize = 32; // 256 bits
    private const int KeySize = 64;  // 512 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    /// <summary>
    /// Genera un hash compuesto en formato "{saltBase64}:{hashBase64}" de 133 caracteres,
    /// almacenable directamente en columnas PasswordHash VARCHAR(255).
    /// </summary>
    public string HashPassword(string password)
    {
        var (hash, salt) = HashPasswordWithSalt(password);
        return $"{salt}:{hash}";
    }

    public string HashPasswordCombined(string password) => HashPassword(password);

    public (string Hash, string Salt) HashPasswordWithSalt(string password)
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

    /// <summary>
    /// Valida la contraseña comparándola contra el hash compuesto almacenado en base de datos.
    /// </summary>
    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        // Formato compuesto {salt}:{hash} o {hash}:{salt}
        if (storedHash.Contains(':'))
        {
            var parts = storedHash.Split(':', 2);
            if (parts[0].Length == 44 && parts[1].Length == 88)
            {
                // {salt}:{hash}
                return VerifyPassword(password, parts[1], parts[0]);
            }
            else if (parts[0].Length == 88 && parts[1].Length == 44)
            {
                // {hash}:{salt}
                return VerifyPassword(password, parts[0], parts[1]);
            }
            else
            {
                // Fallback attempt both
                return VerifyPassword(password, parts[1], parts[0]) || VerifyPassword(password, parts[0], parts[1]);
            }
        }

        return false;
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(salt))
            return false;

        try
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[] expectedHashBytes = Convert.FromBase64String(hash);

            byte[] actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                Algorithm,
                KeySize
            );

            // Comparación en tiempo constante para mitigar ataques de temporización (Timing Attacks)
            return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
