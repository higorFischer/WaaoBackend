using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Video;

public sealed class LiveKitTokenService(IOptions<LiveKitOptions> Options) : ILiveKitTokenService
{
	public string MintToken(LiveKitTokenRequest request)
	{
		var options = Options.Value;

		if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
			throw new InvalidOperationException("LiveKit credentials are not configured. Set LiveKit__ApiKey and LiveKit__ApiSecret.");

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.ApiSecret));
		var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var now = DateTime.UtcNow;
		var exp = now.AddHours(2);

		// LiveKit video grant — embedded as a real JSON object via JsonClaimValueTypes.Json.
		var videoGrant = JsonSerializer.Serialize(new
		{
			room = request.Room,
			roomJoin = true,
			canPublish = true,
			canSubscribe = true,
			canPublishData = true,
		});

		// Participant metadata is an opaque string to LiveKit — WAAO stores the moderator flag as JSON.
		var metadata = JsonSerializer.Serialize(new { moderator = request.Moderator });

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Iss, options.ApiKey),
			new(JwtRegisteredClaimNames.Sub, request.CollaboratorId.ToString()),
			new("name", request.Name),
			new("metadata", metadata),
			new("video", videoGrant, JsonClaimValueTypes.Json),
		};

		var token = new JwtSecurityToken(
			claims: claims,
			notBefore: now,
			expires: exp,
			signingCredentials: signingCredentials);

		token.Payload[JwtRegisteredClaimNames.Iat] = new DateTimeOffset(now).ToUnixTimeSeconds();

		return new JwtSecurityTokenHandler().WriteToken(token);
	}

	public string MintGuestToken(GuestLiveKitTokenRequest request)
	{
		var options = Options.Value;

		if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSecret))
			throw new InvalidOperationException("LiveKit credentials are not configured. Set LiveKit__ApiKey and LiveKit__ApiSecret.");

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.ApiSecret));
		var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var now = DateTime.UtcNow;
		var exp = now.AddHours(2);

		var videoGrant = JsonSerializer.Serialize(new
		{
			room = request.Room,
			roomJoin = true,
			canPublish = true,
			canSubscribe = true,
			canPublishData = true,
			canPublishSources = new[] { "camera", "microphone", "screen_share" },
		});

		var metadata = JsonSerializer.Serialize(new { guest = true });

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Iss, options.ApiKey),
			new(JwtRegisteredClaimNames.Sub, request.Identity),
			new("name", request.Name),
			new("metadata", metadata),
			new("video", videoGrant, JsonClaimValueTypes.Json),
		};

		var token = new JwtSecurityToken(
			claims: claims,
			notBefore: now,
			expires: exp,
			signingCredentials: signingCredentials);

		token.Payload[JwtRegisteredClaimNames.Iat] = new DateTimeOffset(now).ToUnixTimeSeconds();

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
