using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Abstractions.Services;

public interface IManagerNoteService
{
	Task<IReadOnlyList<ManagerNoteDto>> GetForCollaboratorAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default);
	Task<ManagerNoteDto> CreateAsync(Guid collaboratorId, CreateManagerNoteDto dto, Guid callerId, CancellationToken ct = default);
	Task<ManagerNoteDto> UpdateAsync(Guid id, UpdateManagerNoteDto dto, Guid callerId, CancellationToken ct = default);
	Task DeleteAsync(Guid id, Guid callerId, CancellationToken ct = default);
}
