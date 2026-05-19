using System.Security.Cryptography;

namespace Waao.Services.Auth;

/// <summary>
/// PBKDF2-SHA256 password hashing — stdlib-only, no extra deps.
/// Format: v1.iterations.saltB64.hashB64
/// </summary>
public static class PasswordHasher
{
	private const int SaltSize = 16;
	private const int HashSize = 32;
	private const int Iterations = 200_000;

	public static string Hash(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(SaltSize);
		var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
		return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
	}

	public static bool Verify(string password, string stored)
	{
		try
		{
			var parts = stored.Split('.');
			if (parts.Length != 4 || parts[0] != "v1") return false;
			var iterations = int.Parse(parts[1]);
			var salt = Convert.FromBase64String(parts[2]);
			var expected = Convert.FromBase64String(parts[3]);
			var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
			return CryptographicOperations.FixedTimeEquals(expected, actual);
		}
		catch { return false; }
	}
}
