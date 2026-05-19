using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Auth;

public sealed class JwtIssuer(JwtSettings Settings)
{
	public (string token, DateTime expiresAt) Issue(Collaborator c)
	{
		if (string.IsNullOrWhiteSpace(Settings.Key) || Settings.Key.Length < 32)
			throw new InvalidOperationException("JWT key must be at least 32 chars; set Jwt:Key in configuration.");

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Key));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var expires = DateTime.UtcNow.AddMinutes(Settings.ExpiryMinutes);

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, c.Id.ToString()),
			new(JwtRegisteredClaimNames.Email, c.Email),
			new("name", c.FullName),
			new(ClaimTypes.Role, c.RoleKind.ToString()),
		};

		var token = new JwtSecurityToken(
			issuer: Settings.Issuer,
			audience: Settings.Audience,
			claims: claims,
			notBefore: DateTime.UtcNow,
			expires: expires,
			signingCredentials: creds);

		return (new JwtSecurityTokenHandler().WriteToken(token), expires);
	}
}
