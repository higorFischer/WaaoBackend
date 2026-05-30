using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Badges;

namespace Waao.Services.Abstractions.Services;

public interface IBadgeAdminService
{
	/// <summary>Returns all non-deleted manually-created badge definitions.</summary>
	Task<IReadOnlyList<BadgeDto>> ListManualDefinitionsAsync(CancellationToken ct = default);

	/// <summary>Creates a new manual badge definition (admin-only, never auto-awarded).</summary>
	Task<BadgeDto> CreateDefinitionAsync(CreateBadgeDefinitionDto dto, CancellationToken ct = default);

	/// <summary>Updates name/description/icon/color of a manual badge definition. Throws KeyNotFoundException if not found or not manual.</summary>
	Task<BadgeDto> UpdateDefinitionAsync(Guid id, UpdateBadgeDefinitionDto dto, CancellationToken ct = default);

	/// <summary>Soft-deletes a manual badge definition and all its active grants.</summary>
	Task DeleteDefinitionAsync(Guid id, CancellationToken ct = default);

	/// <summary>Manually grants a badge to a collaborator. Notifies recipient best-effort. Returns the flair record.</summary>
	Task<FlairBadgeDto> GrantAsync(GrantBadgeDto dto, Guid awardedById, CancellationToken ct = default);

	/// <summary>Soft-deletes a manual badge grant (revoke). Throws KeyNotFoundException if grant is not for a manual badge.</summary>
	Task RevokeAsync(Guid collaboratorBadgeId, CancellationToken ct = default);

	/// <summary>
	/// Returns the global active-flair map: all non-deleted, non-expired manual grants grouped by collaborator.
	/// Consumed everywhere a collaborator name is displayed.
	/// </summary>
	Task<IReadOnlyList<CollaboratorFlairDto>> GetActiveFlairAsync(CancellationToken ct = default);

	/// <summary>Returns all non-deleted grants for a given manual badge (for the admin manage view; includes expired).</summary>
	Task<IReadOnlyList<FlairBadgeDto>> GetGrantsForBadgeAsync(Guid badgeId, CancellationToken ct = default);
}
