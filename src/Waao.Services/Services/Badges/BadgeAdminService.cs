using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Badges;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.Badges;

public sealed class BadgeAdminService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	ILogger<BadgeAdminService> Logger) : IBadgeAdminService
{
	// =====================================================================
	// LIST DEFINITIONS
	// =====================================================================

	public async Task<IReadOnlyList<BadgeDto>> ListManualDefinitionsAsync(CancellationToken ct = default)
	{
		var badges = await Db.Badges
			.AsNoTracking()
			.Where(b => b.IsManual)
			.OrderBy(b => b.Name)
			.ToListAsync(ct);

		return badges.Select(MapBadgeDto).ToList();
	}

	// =====================================================================
	// CREATE DEFINITION
	// =====================================================================

	public async Task<BadgeDto> CreateDefinitionAsync(CreateBadgeDefinitionDto dto, CancellationToken ct = default)
	{
		var badge = new Badge
		{
			Id = Guid.CreateVersion7(),
			Code = string.Empty,
			Name = dto.Name,
			Description = dto.Description ?? string.Empty,
			IconEmoji = dto.IconEmoji,
			ColorHex = dto.ColorHex,
			Category = BadgeCategory.Special,
			Rarity = BadgeRarity.Common,
			XpReward = 0,
			IsManual = true,
			IsHidden = false,
			CreatedAt = DateTime.UtcNow,
		};

		Db.Badges.Add(badge);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Manual badge definition {Id} ({Name}) created.", badge.Id, badge.Name);

		return MapBadgeDto(badge);
	}

	// =====================================================================
	// UPDATE DEFINITION
	// =====================================================================

	public async Task<BadgeDto> UpdateDefinitionAsync(Guid id, UpdateBadgeDefinitionDto dto, CancellationToken ct = default)
	{
		var badge = await Db.Badges.FirstOrDefaultAsync(b => b.Id == id, ct)
			?? throw new KeyNotFoundException($"Badge {id} not found.");

		if (!badge.IsManual)
			throw new KeyNotFoundException($"Badge {id} is not a manual badge and cannot be updated via this endpoint.");

		badge.Name = dto.Name;
		badge.Description = dto.Description ?? string.Empty;
		badge.IconEmoji = dto.IconEmoji;
		badge.ColorHex = dto.ColorHex;
		badge.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Manual badge definition {Id} updated.", badge.Id);

		return MapBadgeDto(badge);
	}

	// =====================================================================
	// DELETE DEFINITION
	// =====================================================================

	public async Task DeleteDefinitionAsync(Guid id, CancellationToken ct = default)
	{
		var badge = await Db.Badges.FirstOrDefaultAsync(b => b.Id == id, ct)
			?? throw new KeyNotFoundException($"Badge {id} not found.");

		if (!badge.IsManual)
			throw new KeyNotFoundException($"Badge {id} is not a manual badge and cannot be deleted via this endpoint.");

		var now = DateTime.UtcNow;

		// Soft-delete all grants for this badge
		await Db.CollaboratorBadges
			.Where(cb => cb.BadgeId == id)
			.ExecuteUpdateAsync(s => s
				.SetProperty(cb => cb.IsDeleted, true)
				.SetProperty(cb => cb.DeletedAt, now)
				.SetProperty(cb => cb.UpdatedAt, now), ct);

		badge.IsDeleted = true;
		badge.DeletedAt = now;
		badge.UpdatedAt = now;

		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Manual badge definition {Id} soft-deleted along with its grants.", id);
	}

	// =====================================================================
	// GRANT
	// =====================================================================

	public async Task<FlairBadgeDto> GrantAsync(GrantBadgeDto dto, Guid awardedById, CancellationToken ct = default)
	{
		var badge = await Db.Badges
			.AsNoTracking()
			.FirstOrDefaultAsync(b => b.Id == dto.BadgeId, ct)
			?? throw new KeyNotFoundException($"Badge {dto.BadgeId} not found.");

		if (!badge.IsManual)
			throw new InvalidOperationException($"Badge {dto.BadgeId} is not a manual badge and cannot be granted via this endpoint.");

		var now = DateTime.UtcNow;

		var grant = new CollaboratorBadge
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = dto.CollaboratorId,
			BadgeId = dto.BadgeId,
			EarnedAt = now,
			ExpiresAt = dto.ExpiresAt,
			AwardedById = awardedById,
			Context = dto.Note,
			CreatedAt = now,
		};

		Db.CollaboratorBadges.Add(grant);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Manual badge {BadgeId} granted to {CollaboratorId} by {AwardedById}. Grant={GrantId}.",
			dto.BadgeId, dto.CollaboratorId, awardedById, grant.Id);

		// Best-effort notification
		try
		{
			await NotificationService.CreateAsync(
				dto.CollaboratorId,
				NotificationKind.BadgeAwarded,
				"Você recebeu uma insígnia 🏅",
				badge.Name,
				"badge",
				grant.Id,
				awardedById,
				ct);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "Failed to send badge-awarded notification for grant {GrantId}.", grant.Id);
		}

		return MapFlairDto(grant, badge);
	}

	// =====================================================================
	// REVOKE
	// =====================================================================

	public async Task RevokeAsync(Guid collaboratorBadgeId, CancellationToken ct = default)
	{
		var grant = await Db.CollaboratorBadges
			.Include(cb => cb.Badge)
			.FirstOrDefaultAsync(cb => cb.Id == collaboratorBadgeId, ct)
			?? throw new KeyNotFoundException($"Badge grant {collaboratorBadgeId} not found.");

		if (!grant.Badge.IsManual)
			throw new InvalidOperationException($"Grant {collaboratorBadgeId} is not for a manual badge and cannot be revoked via this endpoint.");

		var now = DateTime.UtcNow;
		grant.IsDeleted = true;
		grant.DeletedAt = now;
		grant.UpdatedAt = now;

		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Badge grant {GrantId} revoked.", collaboratorBadgeId);
	}

	// =====================================================================
	// GET ACTIVE FLAIR
	// =====================================================================

	public async Task<IReadOnlyList<CollaboratorFlairDto>> GetActiveFlairAsync(CancellationToken ct = default)
	{
		var now = DateTime.UtcNow;

		var grants = await Db.CollaboratorBadges
			.AsNoTracking()
			.Include(cb => cb.Badge)
			.Where(cb => cb.Badge.IsManual && (cb.ExpiresAt == null || cb.ExpiresAt > now))
			.ToListAsync(ct);

		return grants
			.GroupBy(cb => cb.CollaboratorId)
			.Select(g => new CollaboratorFlairDto
			{
				CollaboratorId = g.Key,
				Badges = g.Select(cb => MapFlairDto(cb, cb.Badge)).ToList(),
			})
			.ToList();
	}

	// =====================================================================
	// GET GRANTS FOR BADGE
	// =====================================================================

	public async Task<IReadOnlyList<FlairBadgeDto>> GetGrantsForBadgeAsync(Guid badgeId, CancellationToken ct = default)
	{
		var badge = await Db.Badges
			.AsNoTracking()
			.FirstOrDefaultAsync(b => b.Id == badgeId, ct)
			?? throw new KeyNotFoundException($"Badge {badgeId} not found.");

		var grants = await Db.CollaboratorBadges
			.AsNoTracking()
			.Where(cb => cb.BadgeId == badgeId)
			.OrderByDescending(cb => cb.EarnedAt)
			.ToListAsync(ct);

		return grants.Select(cb => MapFlairDto(cb, badge)).ToList();
	}

	// =====================================================================
	// MAPPERS
	// =====================================================================

	private static BadgeDto MapBadgeDto(Badge b) => new()
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
		IsManual = b.IsManual,
		ColorHex = b.ColorHex,
	};

	private static FlairBadgeDto MapFlairDto(CollaboratorBadge cb, Badge b) => new()
	{
		Id = cb.Id,
		BadgeId = b.Id,
		Name = b.Name,
		IconEmoji = b.IconEmoji,
		ColorHex = b.ColorHex,
		AwardedAt = cb.EarnedAt,
		ExpiresAt = cb.ExpiresAt,
		Note = cb.Context,
	};
}
