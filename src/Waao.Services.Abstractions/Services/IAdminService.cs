using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface IAdminService
{
	// People management
	Task<CollaboratorDto> PromoteAsync(Guid collaboratorId, PromoteCollaboratorDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> SetRoleKindAsync(Guid collaboratorId, SetRoleKindDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> SetCollaboratorRoleAsync(Guid collaboratorId, SetCollaboratorRoleDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> GrantXpAsync(Guid collaboratorId, GrantXpDto dto, Guid adminId, CancellationToken ct = default);
	Task<CollaboratorDto> CreateUserAsync(AdminCreateUserDto dto, Guid actorId, CancellationToken ct = default);

	// User management
	Task<IReadOnlyList<CollaboratorDto>> ListAllUsersAsync(bool includeDeleted, CancellationToken ct = default);
	Task<CollaboratorDto> AdminUpdateUserAsync(Guid id, AdminUpdateUserDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> AdminResetPasswordAsync(Guid id, AdminResetPasswordDto dto, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> AdminSetStatusAsync(Guid id, AdminSetStatusDto dto, Guid actorId, CancellationToken ct = default);
	Task DeleteUserAsync(Guid id, Guid actorId, CancellationToken ct = default);
	Task<CollaboratorDto> RestoreUserAsync(Guid id, Guid actorId, CancellationToken ct = default);

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
