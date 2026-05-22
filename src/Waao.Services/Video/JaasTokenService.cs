using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Video;

public sealed class JaasTokenService(IOptions<JaasOptions> Options) : IJaasTokenService
{
	public string MintToken(JaasTokenRequest request)
	{
		var options = Options.Value;

		if (string.IsNullOrWhiteSpace(options.PrivateKey))
			throw new InvalidOperationException("JaaS private key is not configured. Set Jaas__PrivateKey.");

		var rsa = RSA.Create();
		rsa.ImportFromPem(options.PrivateKey.AsSpan());

		var rsaKey = new RsaSecurityKey(rsa) { KeyId = options.KeyId };
		var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

		var now = DateTime.UtcNow;
		var exp = now.AddHours(2);

		// Build the context object as a nested JSON claim.
		// JsonClaimValueTypes.Json makes JwtSecurityTokenHandler embed it as a real JSON
		// object (not a quoted string) in the token payload.
		var contextValue = new
		{
			user = new
			{
				id = request.CollaboratorId.ToString(),
				name = request.Name,
				email = request.Email,
				avatar = request.Avatar ?? string.Empty,
				moderator = request.Moderator,
			},
			features = new
			{
				livestreaming = false,
				recording = false,
				transcription = false,
				outbound_call = false,
			},
		};

		var contextJson = JsonSerializer.Serialize(contextValue, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		});

		// Use a custom naming for "outbound-call" — must match JaaS expectation exactly.
		// Re-build the features JSON manually so the key is "outbound-call" (hyphen, not underscore).
		var featuresJson = """{"livestreaming":false,"recording":false,"transcription":false,"outbound-call":false}""";
		var userJson = JsonSerializer.Serialize(new
		{
			id = request.CollaboratorId.ToString(),
			name = request.Name,
			email = request.Email,
			avatar = request.Avatar ?? string.Empty,
			moderator = request.Moderator,
		}, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

		contextJson = $$$"""{"user":{{{userJson}}},"features":{{{featuresJson}}}}""";

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Aud, "jitsi"),
			new(JwtRegisteredClaimNames.Iss, "chat"),
			new(JwtRegisteredClaimNames.Sub, options.AppId),
			new("room", request.Room),
			new("context", contextJson, JsonClaimValueTypes.Json),
		};

		var token = new JwtSecurityToken(
			claims: claims,
			notBefore: now,
			expires: exp,
			signingCredentials: signingCredentials);

		// Ensure iat is present (JwtSecurityToken doesn't add it by default via this ctor)
		var iatValue = new DateTimeOffset(now).ToUnixTimeSeconds();
		token.Payload[JwtRegisteredClaimNames.Iat] = iatValue;

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
