namespace Waao.Services.Abstractions.Dtos;

public record CareerEventCreatedDto
{
	public CareerEventDto Event { get; init; } = new();
	public int XpAwarded { get; init; }
	public int StreakBonusXp { get; init; }
	public int CurrentStreakDays { get; init; }
	public IReadOnlyList<BadgeDto> NewBadges { get; init; } = [];
	public int LevelBefore { get; init; }
	public int LevelAfter { get; init; }
	public bool LevelUp => LevelAfter > LevelBefore;
}
