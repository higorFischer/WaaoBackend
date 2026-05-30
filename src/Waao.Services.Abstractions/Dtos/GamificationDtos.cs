using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos;

public record BadgeDto
{
	public Guid Id { get; init; }
	public string Code { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string IconEmoji { get; init; } = "🏅";
	public string? IconUrl { get; init; }
	public BadgeCategory Category { get; init; }
	public BadgeRarity Rarity { get; init; }
	public int XpReward { get; init; }
	public string? UnlockRule { get; init; }
	public bool IsManual { get; init; }
	public string? ColorHex { get; init; }
}

public record CollaboratorBadgeDto
{
	public Guid Id { get; init; }
	public BadgeDto Badge { get; init; } = new();
	public DateTime EarnedAt { get; init; }
	public string? Context { get; init; }
	public DateTime? ExpiresAt { get; init; }
}

public record LevelProgressDto
{
	public int CurrentLevel { get; init; }
	public string CurrentTitle { get; init; } = string.Empty;
	public string CurrentIcon { get; init; } = "⭐";
	public long CurrentXp { get; init; }
	public long XpIntoLevel { get; init; }
	public long XpForNextLevel { get; init; }
	public int ProgressPercent { get; init; }
	public int? NextLevel { get; init; }
	public string? NextTitle { get; init; }
}

public record LeaderboardEntryDto
{
	public int Rank { get; init; }
	public Guid CollaboratorId { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public string? DepartmentName { get; init; }
	public long Xp { get; init; }
	public int Level { get; init; }
	public int BadgeCount { get; init; }
}

public record XpTransactionDto
{
	public Guid Id { get; init; }
	public int Amount { get; init; }
	public XpSource Source { get; init; }
	public string Reason { get; init; } = string.Empty;
	public DateTime OccurredAt { get; init; }
}
