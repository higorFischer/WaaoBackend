using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Rules;

/// <summary>
/// Central XP reward table. Change values here to rebalance the gamification economy.
/// </summary>
public static class XpRules
{
	public static int XpForCareerEvent(CareerEventType type) => type switch
	{
		CareerEventType.Hire => 100,
		CareerEventType.Promotion => 500,
		CareerEventType.Lateral => 200,
		CareerEventType.SalaryChange => 50,
		CareerEventType.TitleChange => 100,
		CareerEventType.DepartmentChange => 150,
		CareerEventType.Training => 100,
		CareerEventType.Certification => 250,
		CareerEventType.PerformanceReview => 150,
		CareerEventType.TenureMilestone => 300,
		CareerEventType.Kudos => 25,
		CareerEventType.Termination => 0,
		_ => 0,
	};

	public static int StreakBonus(int streakDays) => streakDays switch
	{
		>= 365 => 500,
		>= 180 => 250,
		>= 90 => 100,
		>= 30 => 50,
		>= 7 => 10,
		_ => 0,
	};

	// ----- Kanban ---------------------------------------------------------
	public static int XpForCardCompleted(CardPriority priority, int? storyPoints) => priority switch
	{
		CardPriority.Critical => 100 + (storyPoints ?? 0) * 15,
		CardPriority.High     => 50  + (storyPoints ?? 0) * 12,
		CardPriority.Medium   => 25  + (storyPoints ?? 0) * 10,
		CardPriority.Low      => 10  + (storyPoints ?? 0) * 5,
		_ => 25,
	};

	public const int XpForCommentOnOthersCard = 5;
	public const int XpForEpicCompleted = 200;
}
