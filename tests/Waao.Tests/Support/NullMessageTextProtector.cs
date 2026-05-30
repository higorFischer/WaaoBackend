using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>Passthrough IMessageTextProtector for tests — bodies are stored/returned verbatim (no encryption).</summary>
public sealed class NullMessageTextProtector : IMessageTextProtector
{
	public static readonly NullMessageTextProtector Instance = new();

	public bool IsEnabled => false;

	public string Protect(string plaintext) => plaintext;

	public string? Unprotect(string? stored) => stored;
}
