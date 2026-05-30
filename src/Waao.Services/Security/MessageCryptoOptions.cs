namespace Waao.Services.Security;

/// <summary>Config for message body encryption at rest. <see cref="Key"/> is a base64-encoded 256-bit
/// (32-byte) key, supplied in prod as the Fly secret <c>MessageCrypto__Key</c>. Empty = encryption off.</summary>
public sealed class MessageCryptoOptions
{
	public string Key { get; set; } = string.Empty;
}
