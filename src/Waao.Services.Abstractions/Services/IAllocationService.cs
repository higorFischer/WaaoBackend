using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Abstractions.Services;

public interface IAllocationService
{
	Task<AllocationBoardDto> GetBoardAsync(CancellationToken ct = default);
	Task<IReadOnlyList<ProjectWithAllocationsDto>> GetByCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default);

	Task<ProjectWithAllocationsDto> CreateProjectAsync(CreateProjectDto dto, CancellationToken ct = default);
	Task<ProjectWithAllocationsDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto, CancellationToken ct = default);
	Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default);
	Task ReorderProjectsAsync(ReorderProjectsDto dto, CancellationToken ct = default);

	Task<AllocationDto> AllocateAsync(CreateAllocationDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<AllocationDto> MoveAllocationAsync(Guid allocationId, MoveAllocationDto dto, CancellationToken ct = default);
	Task<AllocationDto> UpdateNoteAsync(Guid allocationId, UpdateNoteDto dto, CancellationToken ct = default);
	Task RemoveAllocationAsync(Guid allocationId, CancellationToken ct = default);
}
