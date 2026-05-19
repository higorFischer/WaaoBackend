using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Domain.Models.Rules;
using Waao.Infra.EF;

namespace Waao.Services.Gamification;

/// <summary>
/// Central engine for all XP awards and level progression.
/// Keeps the gamification economy in one place — any service that grants XP calls this.
/// </summary>
public sealed class GamificationEngine(WaaoDbContext Db)
{
	public async Task<int> AwardCareerEventXpAsync(CareerEvent evt, CancellationToken ct = default)
	{
		var amount = XpRules.XpForCareerEvent(evt.Type);
		if (amount <= 0) return 0;

		var reason = $"{evt.Type}: {evt.Title}";
		await RecordAsync(evt.CollaboratorId, amount, XpSource.CareerEvent, reason, evt.Id, nameof(CareerEvent), ct);
		evt.XpAwarded = amount;
		return amount;
	}

	public async Task RecordAsync(
		Guid collaboratorId,
		int amount,
		XpSource source,
		string reason,
		Guid? sourceEntityId = null,
		string? sourceEntityType = null,
		CancellationToken ct = default)
	{
		if (amount == 0) return;

		Db.XpTransactions.Add(new XpTransaction
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = collaboratorId,
			Amount = amount,
			Source = source,
			Reason = reason,
			SourceEntityId = sourceEntityId,
			SourceEntityType = sourceEntityType,
			OccurredAt = DateTime.UtcNow,
		});

		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct);
		if (collaborator is null) return;

		collaborator.TotalXp += amount;
		collaborator.CurrentLevel = await ComputeLevelAsync(collaborator.TotalXp, ct);
	}

	public async Task<int> ComputeLevelAsync(long totalXp, CancellationToken ct = default)
	{
		var definitions = await Db.LevelDefinitions
			.OrderBy(l => l.Level)
			.ToListAsync(ct);

		if (definitions.Count == 0)
			return Math.Max(1, (int)(totalXp / 500) + 1);   // fallback: flat 500 XP per level

		var lvl = 1;
		foreach (var def in definitions)
			if (totalXp >= def.XpThreshold) lvl = def.Level;
		return lvl;
	}
}
