using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface IAdminService
{
	// People management
	Task<CollaboratorDto> PromoteAsync(Guid collaboratorId, PromoteCollaboratorDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> SetRoleKindAsync(Guid collaboratorId, SetRoleKindDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> SetCollaboratorRoleAsync(Guid collaboratorId, SetCollaboratorRoleDto dto, Guid actorId, CancellationToken ct = default);

	// Job roles
	Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(CancellationToken ct = default);
	Task<JobRoleDto> CreateJobRoleAsync(CreateJobRoleDto dto, CancellationToken ct = default);
	Task<JobRoleDto> UpdateJobRoleAsync(Guid id, UpdateJobRoleDto dto, CancellationToken ct = default);
	Task DeleteJobRoleAsync(Guid id, CancellationToken ct = default);

	// Departments
	Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct = default);
	Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto, CancellationToken ct = default);
	Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto, CancellationToken ct = default);
	Task DeleteDepartmentAsync(Guid id, CancellationToken ct = default);

	// Level definitions
	Task<IReadOnlyList<LevelDefinitionDto>> ListLevelsAsync(CancellationToken ct = default);
	Task<LevelDefinitionDto> UpsertLevelAsync(UpsertLevelDefinitionDto dto, CancellationToken ct = default);
	Task DeleteLevelAsync(Guid id, CancellationToken ct = default);
}
