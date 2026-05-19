using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos;

public record LoginDto
{
	public string Email { get; init; } = string.Empty;
	public string Password { get; init; } = string.Empty;
}

public record RegisterDto
{
	public string FullName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public string Password { get; init; } = string.Empty;
	public DateOnly JoinDate { get; init; }
	public Guid? DepartmentId { get; init; }
	public Guid? RoleId { get; init; }
}

public record AuthResultDto
{
	public string Token { get; init; } = string.Empty;
	public DateTime ExpiresAt { get; init; }
	public CollaboratorDto Me { get; init; } = new();
	public IReadOnlyList<BadgeDto> NewBadges { get; init; } = [];
	public int LoginStreakDays { get; init; }
	public int LoginStreakBonusXp { get; init; }
}

public record ChangePasswordDto
{
	public string CurrentPassword { get; init; } = string.Empty;
	public string NewPassword { get; init; } = string.Empty;
}

public record VerifyEmailDto
{
	public string Token { get; init; } = string.Empty;
}

public record ResendVerificationDto
{
	public string Email { get; init; } = string.Empty;
}

public record RegisterResultDto
{
	public string Status { get; init; } = "verification_sent";
	public string Email { get; init; } = string.Empty;
}

public record JwtSettings
{
	public string Issuer { get; init; } = "waao";
	public string Audience { get; init; } = "waao-frontend";
	public string Key { get; init; } = string.Empty;
	public int ExpiryMinutes { get; init; } = 60 * 8;
}
