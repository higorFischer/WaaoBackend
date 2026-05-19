using Microsoft.EntityFrameworkCore;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class GamificationService(WaaoDbContext Db) : IGamificationService
{
	public async Task<LevelProgressDto> GetLevelProgressAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var collab = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found");

		var levels = await Db.LevelDefinitions.OrderBy(l => l.Level).ToListAsync(ct);
		if (levels.Count == 0) return Fallback(collab.TotalXp);

		// Same semantic as GamificationEngine.ComputeLevelAsync: level 0 is the floor.
		// Only positive-threshold definitions count as a "reached" level — a
		// zero-threshold seed (e.g. L1 "Newcomer" @ 0) never lifts a 0-XP
		// collaborator off level 0.
		var current = levels.LastOrDefault(l => l.XpThreshold > 0 && collab.TotalXp >= l.XpThreshold);

		if (current is null)
		{
			// Level 0: progress runs toward the lowest positive-threshold level.
			var firstPositive = levels.FirstOrDefault(l => l.XpThreshold > 0);
			if (firstPositive is null) return Fallback(collab.TotalXp);

			var xpForNext0 = firstPositive.XpThreshold;
			var pct0 = xpForNext0 == 0 ? 100 : (int)(collab.TotalXp * 100 / xpForNext0);

			return new LevelProgressDto
			{
				CurrentLevel = 0,
				CurrentTitle = "Unranked",
				CurrentIcon = "⭐",
				CurrentXp = collab.TotalXp,
				XpIntoLevel = collab.TotalXp,
				XpForNextLevel = xpForNext0,
				ProgressPercent = Math.Clamp(pct0, 0, 100),
				NextLevel = firstPositive.Level,
				NextTitle = firstPositive.Title,
			};
		}

		var next = levels.FirstOrDefault(l => l.Level > current.Level);

		var xpInto = collab.TotalXp - current.XpThreshold;
		var xpForNext = next is null ? 0 : next.XpThreshold - current.XpThreshold;
		var pct = xpForNext == 0 ? 100 : (int)(xpInto * 100 / xpForNext);

		return new LevelProgressDto
		{
			CurrentLevel = current.Level,
			CurrentTitle = current.Title,
			CurrentIcon = current.IconEmoji,
			CurrentXp = collab.TotalXp,
			XpIntoLevel = xpInto,
			XpForNextLevel = xpForNext,
			ProgressPercent = Math.Clamp(pct, 0, 100),
			NextLevel = next?.Level,
			NextTitle = next?.Title,
		};
	}

	private static LevelProgressDto Fallback(long totalXp)
	{
		// No usable level definitions => level 0 is the floor (consistent with
		// GamificationEngine.ComputeLevelAsync). 0 XP stays level 0; a flat
		// 500-XP band still drives the progress bar above the floor.
		const int band = 500;
		var lvl = (int)(totalXp / band);
		var into = totalXp % band;
		return new LevelProgressDto
		{
			CurrentLevel = lvl,
			CurrentTitle = lvl == 0 ? "Unranked" : $"Level {lvl}",
			CurrentIcon = "⭐",
			CurrentXp = totalXp,
			XpIntoLevel = into,
			XpForNextLevel = band,
			ProgressPercent = Math.Clamp((int)(into * 100 / band), 0, 100),
			NextLevel = lvl + 1,
			NextTitle = $"Level {lvl + 1}",
		};
	}

	public async Task<IReadOnlyList<CollaboratorBadgeDto>> GetBadgesAsync(Guid collaboratorId, CancellationToken ct = default)
		=> await Db.CollaboratorBadges
			.Include(cb => cb.Badge)
			.Where(cb => cb.CollaboratorId == collaboratorId)
			.OrderByDescending(cb => cb.EarnedAt)
			.Select(cb => new CollaboratorBadgeDto
			{
				Id = cb.Id,
				EarnedAt = cb.EarnedAt,
				Context = cb.Context,
				Badge = new BadgeDto
				{
					Id = cb.Badge.Id,
					Code = cb.Badge.Code,
					Name = cb.Badge.Name,
					Description = cb.Badge.Description,
					IconEmoji = cb.Badge.IconEmoji,
					IconUrl = cb.Badge.IconUrl,
					Category = cb.Badge.Category,
					Rarity = cb.Badge.Rarity,
					XpReward = cb.Badge.XpReward,
					UnlockRule = cb.Badge.UnlockRule,
				},
			})
			.ToListAsync(ct);

	public async Task<IReadOnlyList<XpTransactionDto>> GetXpHistoryAsync(Guid collaboratorId, int take = 50, CancellationToken ct = default)
		=> await Db.XpTransactions
			.Where(x => x.CollaboratorId == collaboratorId)
			.OrderByDescending(x => x.OccurredAt)
			.Take(take)
			.Select(x => new XpTransactionDto
			{
				Id = x.Id,
				Amount = x.Amount,
				Source = x.Source,
				Reason = x.Reason,
				OccurredAt = x.OccurredAt,
			})
			.ToListAsync(ct);

	public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int take = 20, CancellationToken ct = default)
	{
		var rows = await Db.Collaborators
			.Where(c => c.OptInLeaderboards && c.Status == Waao.Domain.Models.Enums.CollaboratorStatus.Active)
			.Include(c => c.Department)
			.Include(c => c.Badges)
			.OrderByDescending(c => c.TotalXp)
			.Take(take)
			.ToListAsync(ct);

		return rows.Select((c, i) => new LeaderboardEntryDto
		{
			Rank = i + 1,
			CollaboratorId = c.Id,
			FullName = c.FullName,
			PhotoUrl = c.PhotoUrl,
			DepartmentName = c.Department?.Name,
			Xp = c.TotalXp,
			Level = c.CurrentLevel,
			BadgeCount = c.Badges?.Count ?? 0,
		}).ToList();
	}

	public async Task<IReadOnlyList<BadgeDto>> GetAllBadgesAsync(CancellationToken ct = default)
		=> await Db.Badges
			.Where(b => !b.IsHidden)
			.OrderBy(b => b.Category).ThenBy(b => b.Rarity).ThenBy(b => b.Name)
			.Select(b => new BadgeDto
			{
				Id = b.Id,
				Code = b.Code,
				Name = b.Name,
				Description = b.Description,
				IconEmoji = b.IconEmoji,
				IconUrl = b.IconUrl,
				Category = b.Category,
				Rarity = b.Rarity,
				XpReward = b.XpReward,
				UnlockRule = b.UnlockRule,
			})
			.ToListAsync(ct);
}
