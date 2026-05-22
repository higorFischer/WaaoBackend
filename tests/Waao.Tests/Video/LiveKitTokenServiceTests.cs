using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Video;
using Xunit;

namespace Waao.Tests.Video;

public class LiveKitTokenServiceTests
{
	private const string ApiKey = "APIwaaoTest01";
	private const string ApiSecret = "test-secret-0123456789-abcdefghij-XYZ";

	private static LiveKitTokenService Build() =>
		new(Options.Create(new LiveKitOptions
		{
			Url = "wss://waao-livekit.fly.dev",
			ApiKey = ApiKey,
			ApiSecret = ApiSecret,
		}));

	private static LiveKitTokenRequest MakeRequest(bool moderator = true) => new()
	{
		CollaboratorId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
		Name = "Jane Doe",
		Room = "waao-abc123",
		Moderator = moderator,
	};

	[Fact]
	public void MintToken_IsSignedWithApiSecret_HS256()
	{
		var jwt = Build().MintToken(MakeRequest());

		var parameters = new TokenValidationParameters
		{
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateLifetime = false,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiSecret)),
		};

		var act = () => new JwtSecurityTokenHandler().ValidateToken(jwt, parameters, out _);
		act.Should().NotThrow();
	}

	[Fact]
	public void MintToken_SetsIssuerToApiKeyAndSubjectToCollaboratorId()
	{
		var token = new JwtSecurityTokenHandler().ReadJwtToken(Build().MintToken(MakeRequest()));

		token.Issuer.Should().Be(ApiKey);
		token.Subject.Should().Be("11111111-1111-1111-1111-111111111111");
	}

	[Fact]
	public void MintToken_EmbedsVideoGrantWithJoinAndPublishRights()
	{
		var token = new JwtSecurityTokenHandler().ReadJwtToken(Build().MintToken(MakeRequest()));

		var video = token.Claims.Single(c => c.Type == "video").Value;
		using var doc = JsonDocument.Parse(video);
		doc.RootElement.GetProperty("room").GetString().Should().Be("waao-abc123");
		doc.RootElement.GetProperty("roomJoin").GetBoolean().Should().BeTrue();
		doc.RootElement.GetProperty("canPublish").GetBoolean().Should().BeTrue();
		doc.RootElement.GetProperty("canSubscribe").GetBoolean().Should().BeTrue();
	}

	[Fact]
	public void MintToken_EncodesModeratorFlagInMetadata()
	{
		var token = new JwtSecurityTokenHandler().ReadJwtToken(Build().MintToken(MakeRequest(moderator: true)));

		var metadata = token.Claims.Single(c => c.Type == "metadata").Value;
		using var doc = JsonDocument.Parse(metadata);
		doc.RootElement.GetProperty("moderator").GetBoolean().Should().BeTrue();
	}

	[Fact]
	public void MintToken_WithoutCredentials_Throws()
	{
		var svc = new LiveKitTokenService(Options.Create(new LiveKitOptions()));

		var act = () => svc.MintToken(MakeRequest());

		act.Should().Throw<InvalidOperationException>();
	}
}
