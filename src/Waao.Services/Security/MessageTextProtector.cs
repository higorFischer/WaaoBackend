using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Security;

/// <summary>
/// AES-256-GCM message body protector. Stored form is <c>enc:1:</c> + base64(nonce(12) | ciphertext | tag(16)).
/// Authenticated encryption (GCM) so tampering is detected. A random nonce per message means identical
/// plaintexts never produce identical ciphertext.
/// </summary>
public sealed class MessageTextProtector : IMessageTextProtector
{
	private const string Prefix = "enc:1:";
	private const int NonceSize = 12;
	private const int TagSize = 16;

	private readonly byte[]? Key;

	public MessageTextProtector(IOptions<MessageCryptoOptions> Options, ILogger<MessageTextProtector> Logger)
	{
		var raw = Options.Value.Key;
		if (string.IsNullOrWhiteSpace(raw))
		{
			Logger.LogWarning("MessageCrypto key not configured — message bodies are stored as PLAINTEXT.");
			return;
		}

		try
		{
			var bytes = Convert.FromBase64String(raw.Trim());
			if (bytes.Length == 32)
			{
				Key = bytes;
				Logger.LogInformation("Message body encryption at rest is ENABLED (AES-256-GCM).");
			}
			else
			{
				Logger.LogError("MessageCrypto key must be 32 bytes (base64), got {Length} — encryption DISABLED.", bytes.Length);
			}
		}
		catch (FormatException)
		{
			Logger.LogError("MessageCrypto key is not valid base64 — encryption DISABLED.");
		}
	}

	public bool IsEnabled => Key is not null;

	public string Protect(string plaintext)
	{
		if (Key is null || string.IsNullOrEmpty(plaintext)) return plaintext;

		var nonce = RandomNumberGenerator.GetBytes(NonceSize);
		var pt = Encoding.UTF8.GetBytes(plaintext);
		var ct = new byte[pt.Length];
		var tag = new byte[TagSize];

		using var gcm = new AesGcm(Key, TagSize);
		gcm.Encrypt(nonce, pt, ct, tag);

		var blob = new byte[NonceSize + ct.Length + TagSize];
		Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
		Buffer.BlockCopy(ct, 0, blob, NonceSize, ct.Length);
		Buffer.BlockCopy(tag, 0, blob, NonceSize + ct.Length, TagSize);
		return Prefix + Convert.ToBase64String(blob);
	}

	public string? Unprotect(string? stored)
	{
		if (stored is null) return null;
		// Legacy plaintext (no prefix) or encryption disabled → return as-is.
		if (Key is null || !stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;

		try
		{
			var blob = Convert.FromBase64String(stored[Prefix.Length..]);
			if (blob.Length < NonceSize + TagSize) return stored;

			var nonce = blob.AsSpan(0, NonceSize);
			var ct = blob.AsSpan(NonceSize, blob.Length - NonceSize - TagSize);
			var tag = blob.AsSpan(blob.Length - TagSize, TagSize);
			var pt = new byte[ct.Length];

			using var gcm = new AesGcm(Key, TagSize);
			gcm.Decrypt(nonce, ct, tag, pt);
			return Encoding.UTF8.GetString(pt);
		}
		catch (Exception)
		{
			// Wrong key / corrupted / tampered — fail safe to the raw value so the chat never crashes.
			return stored;
		}
	}
}
