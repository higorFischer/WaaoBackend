using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Abstractions.Services;

public interface ISkillService
{
	// ----- Catalog (Admin/HR) -----
	Task<IReadOnlyList<SkillDto>> GetCatalogAsync(bool includeArchived = false, CancellationToken ct = default);
	Task<SkillDto> CreateAsync(CreateSkillDto dto, CancellationToken ct = default);
	Task<SkillDto> UpdateAsync(Guid id, UpdateSkillDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);

	// ----- Per-collaborator assessments (guarded by ManagerAccess) -----
	Task<IReadOnlyList<CollaboratorSkillDto>> GetForCollaboratorAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default);
	Task<CollaboratorSkillDto> UpsertForCollaboratorAsync(Guid collaboratorId, Guid skillId, UpsertCollaboratorSkillDto dto, Guid callerId, CancellationToken ct = default);
	Task RemoveForCollaboratorAsync(Guid collaboratorId, Guid skillId, Guid callerId, CancellationToken ct = default);
}
