namespace Waao.Services.Abstractions.Services;

/// <summary>
/// Encrypts/decrypts message bodies at rest with AES-256-GCM. Encrypted values carry an
/// <c>enc:1:</c> prefix so legacy plaintext rows stay readable (graceful, no backfill) and
/// future key rotation is possible. When no key is configured (e.g. local dev) it is a no-op
/// passthrough, so plaintext rows and the feature degrade safely.
/// </summary>
public interface IMessageTextProtector
{
	/// <summary>True when a valid 256-bit key is configured and encryption is active.</summary>
	bool IsEnabled { get; }

	/// <summary>Encrypt a plaintext body for storage. Returns the input unchanged if disabled or empty.</summary>
	string Protect(string plaintext);

	/// <summary>Decrypt a stored body for display. Returns the input unchanged for legacy plaintext
	/// (no prefix) or when disabled; null passes through as null.</summary>
	string? Unprotect(string? stored);
}
