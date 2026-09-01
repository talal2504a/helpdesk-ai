namespace HelpDesk.Infrastructure.Security;

/// <summary>PBKDF2 (SHA-256, 100k iterations) password hashing. Format: iterations.salt.hash (Base64).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898(password, salt, Iterations);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.', 3);
            if (parts.Length != 3) return false;
            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898(password, salt, iterations);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch { return false; }
    }

    private static byte[] Rfc2898(string password, byte[] salt, int iterations)
    {
        // PBKDF2-HMAC-SHA256 via Rfc2898DeriveBytes.Pbkdf2 (.NET 6+)
        return System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(password), salt, iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256, KeySize);
    }
}