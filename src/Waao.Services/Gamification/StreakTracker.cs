using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Domain.Models.Rules;
using Waao.Infra.EF;

namespace Waao.Services.Gamification;

/// <summary>
/// Updates a collaborator's streak state after any interaction.
/// Rule: same-day = no change. +1 day = streak continues. >1 day = reset to 1.
/// When the streak crosses a threshold in XpRules.StreakBonus, awards the bonus once.
///
/// Two streams:
///  - Activity streak (career events) — RegisterActivityAsync
///  - Login streak (auth)              — RegisterLoginAsync
/// </summary>
public sealed class StreakTracker(WaaoDbContext Db, GamificationEngine Gamification)
{
	public async Task<(int current, int longest, int bonusAwarded)> RegisterActivityAsync(
		Guid collaboratorId, DateOnly? activityDate = null, CancellationToken ct = default)
	{
		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct);
		if (collaborator is null) return (0, 0, 0);

		var today = activityDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
		var last = collaborator.LastActivityDate;
		var prevStreak = collaborator.CurrentStreakDays;

		collaborator.CurrentStreakDays = StepStreak(prevStreak, last, today);
		collaborator.LastActivityDate = today;
		if (collaborator.CurrentStreakDays > collaborator.LongestStreakDays)
			collaborator.LongestStreakDays = collaborator.CurrentStreakDays;

		var bonus = await AwardThresholdBonus(
			collaboratorId, prevStreak, collaborator.CurrentStreakDays, "activity streak", ct);
		return (collaborator.CurrentStreakDays, collaborator.LongestStreakDays, bonus);
	}

	public async Task<(int current, int longest, int bonusAwarded)> RegisterLoginAsync(
		Guid collaboratorId, DateOnly? loginDate = null, CancellationToken ct = default)
	{
		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct);
		if (collaborator is null) return (0, 0, 0);

		var today = loginDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
		var last = collaborator.LastLoginDate;
		var prevStreak = collaborator.CurrentLoginStreakDays;

		collaborator.CurrentLoginStreakDays = StepStreak(prevStreak, last, today);
		collaborator.LastLoginDate = today;
		collaborator.LastLoginAt = DateTime.UtcNow;
		if (collaborator.CurrentLoginStreakDays > collaborator.LongestLoginStreakDays)
			collaborator.LongestLoginStreakDays = collaborator.CurrentLoginStreakDays;

		var bonus = await AwardThresholdBonus(
			collaboratorId, prevStreak, collaborator.CurrentLoginStreakDays, "login streak", ct);
		return (collaborator.CurrentLoginStreakDays, collaborator.LongestLoginStreakDays, bonus);
	}

	private static int StepStreak(int prev, DateOnly? last, DateOnly today)
	{
		if (last is null) return 1;
		var delta = today.DayNumber - last.Value.DayNumber;
		return delta switch
		{
			0 => Math.Max(1, prev),
			1 => prev + 1,
			_ => 1,
		};
	}

	private async Task<int> AwardThresholdBonus(
		Guid collaboratorId, int prevStreak, int currentStreak, string label, CancellationToken ct)
	{
		var bonus = 0;
		int[] thresholds = [7, 30, 90, 180, 365];
		foreach (var t in thresholds)
		{
			if (currentStreak >= t && prevStreak < t)
			{
				var delta = XpRules.StreakBonus(t);
				if (delta > 0)
				{
					await Gamification.RecordAsync(
						collaboratorId, delta, XpSource.StreakBonus,
						$"{t}-day {label} bonus", collaboratorId, "Streak", ct);
					bonus += delta;
				}
			}
		}
		return bonus;
	}
}
