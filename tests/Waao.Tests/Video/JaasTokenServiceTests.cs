using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Video;
using Xunit;

namespace Waao.Tests.Video;

public class JaasTokenServiceTests
{
	private static (JaasTokenService svc, string publicKeyPem) Build(bool moderator = true)
	{
		// Generate a throwaway RSA keypair for testing
		using var tempRsa = RSA.Create(2048);
		var privatePem = tempRsa.ExportRSAPrivateKeyPem();
		var publicPem = tempRsa.ExportRSAPublicKeyPem();

		var options = Options.Create(new JaasOptions
		{
			AppId = "test-app-id",
			KeyId = "test-app-id/abc123",
			PrivateKey = privatePem,
		});

		return (new JaasTokenService(options), publicPem);
	}

	private static JaasTokenRequest MakeRequest(bool moderator = true) => new()
	{
		CollaboratorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
		Name = "Jane Doe",
		Email = "jane@example.com",
		Avatar = null,
		Room = "waao-abc123",
		Moderator = moderator,
	};

	[Fact]
	public void MintToken_Header_HasKidAndAlgRS256()
	{
		var (svc, _) = Build();
		var raw = svc.MintToken(MakeRequest());

		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.ReadJwtToken(raw);

		jwt.Header.Kid.Should().Be("test-app-id/abc123");
		jwt.Header.Alg.Should().Be("RS256");
	}

	[Fact]
	public void MintToken_Payload_HasExpectedStandardClaims()
	{
		var (svc, _) = Build();
		var raw = svc.MintToken(MakeRequest());

		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.ReadJwtToken(raw);

		jwt.Audiences.Should().Contain("jitsi");
		jwt.Payload["iss"].Should().Be("chat");
		jwt.Payload["sub"].Should().Be("test-app-id");
		jwt.Payload["room"].Should().Be("waao-abc123");
	}

	[Fact]
	public void MintToken_Context_IsNestedJsonObject_NotAString()
	{
		var (svc, _) = Build(moderator: true);
		var raw = svc.MintToken(MakeRequest(moderator: true));

		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.ReadJwtToken(raw);

		// The context claim must be a nested object, not a string
		jwt.Payload.TryGetValue("context", out var contextObj).Should().BeTrue();
		contextObj.Should().NotBeOfType<string>("context must serialize as a JSON object, not a quoted string");

		// Parse the context to verify nested structure
		var contextJson = JsonSerializer.Serialize(contextObj);
		var doc = JsonDocument.Parse(contextJson);

		doc.RootElement.TryGetProperty("user", out var userEl).Should().BeTrue();
		doc.RootElement.TryGetProperty("features", out _).Should().BeTrue();

		userEl.GetProperty("moderator").GetBoolean().Should().BeTrue();
		userEl.GetProperty("name").GetString().Should().Be("Jane Doe");
		userEl.GetProperty("email").GetString().Should().Be("jane@example.com");
	}

	[Fact]
	public void MintToken_ModeratorFalse_ContextUserModeratorIsFalse()
	{
		var (svc, _) = Build(moderator: false);
		var raw = svc.MintToken(MakeRequest(moderator: false));

		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.ReadJwtToken(raw);

		var contextJson = JsonSerializer.Serialize(jwt.Payload["context"]);
		var doc = JsonDocument.Parse(contextJson);
		doc.RootElement.GetProperty("user").GetProperty("moderator").GetBoolean().Should().BeFalse();
	}

	[Fact]
	public void MintToken_Signature_VerifiesWithCorrespondingPublicKey()
	{
		var (svc, publicPem) = Build();
		var raw = svc.MintToken(MakeRequest());

		using var verifyRsa = RSA.Create();
		verifyRsa.ImportFromPem(publicPem.AsSpan());

		var handler = new JwtSecurityTokenHandler();
		var rsaKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(verifyRsa);
		var validationParams = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
		{
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateLifetime = true,
			IssuerSigningKey = rsaKey,
			ClockSkew = TimeSpan.Zero,
		};

		var act = () => handler.ValidateToken(raw, validationParams, out _);
		act.Should().NotThrow();
	}

	[Fact]
	public void MintToken_Features_HasOutboundCallKey()
	{
		var (svc, _) = Build();
		var raw = svc.MintToken(MakeRequest());

		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.ReadJwtToken(raw);

		var contextJson = JsonSerializer.Serialize(jwt.Payload["context"]);
		contextJson.Should().Contain("outbound-call");

		var doc = JsonDocument.Parse(contextJson);
		doc.RootElement.GetProperty("features").TryGetProperty("outbound-call", out var outboundCall).Should().BeTrue();
		outboundCall.GetBoolean().Should().BeFalse();
	}
}
