using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Waao.Services.Security;
using Xunit;

namespace Waao.Tests.Messaging;

public class MessageTextProtectorTests
{
	private static MessageTextProtector WithKey()
	{
		var key = Convert.ToBase64String(new byte[32]); // all-zero 256-bit key — test only
		return new MessageTextProtector(Options.Create(new MessageCryptoOptions { Key = key }), NullLogger<MessageTextProtector>.Instance);
	}

	[Fact]
	public void Protect_ThenUnprotect_Roundtrips()
	{
		var p = WithKey();
		p.IsEnabled.Should().BeTrue();

		var plain = "Olá @[Maria](abc) — segredo 🤫 com acentuação";
		var enc = p.Protect(plain);

		enc.Should().StartWith("enc:1:");
		enc.Should().NotContain("segredo");
		p.Unprotect(enc).Should().Be(plain);
	}

	[Fact]
	public void Unprotect_PassesThroughLegacyPlaintextAndNull()
	{
		var p = WithKey();
		p.Unprotect("legacy plain body").Should().Be("legacy plain body");
		p.Unprotect(null).Should().BeNull();
	}

	[Fact]
	public void NoKey_IsDisabledAndPassthrough()
	{
		var p = new MessageTextProtector(Options.Create(new MessageCryptoOptions { Key = "" }), NullLogger<MessageTextProtector>.Instance);
		p.IsEnabled.Should().BeFalse();
		p.Protect("hello").Should().Be("hello");
		p.Unprotect("hello").Should().Be("hello");
	}

	[Fact]
	public void SamePlaintext_YieldsDifferentCiphertext()
	{
		var p = WithKey();
		p.Protect("repeat").Should().NotBe(p.Protect("repeat")); // random per-message nonce
	}

	[Fact]
	public void TamperedCiphertext_FailsSafeToRaw()
	{
		var p = WithKey();
		var tampered = "enc:1:not-valid-base64!!!";
		p.Unprotect(tampered).Should().Be(tampered); // never throws — chat must not crash
	}
}
