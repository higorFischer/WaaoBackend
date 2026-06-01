using Waao.Services.Abstractions.Dtos.OneOnOnes;

namespace Waao.Services.Abstractions.Services;

public interface IOneOnOneService
{
	Task<IReadOnlyList<OneOnOneDto>> ListMineAsync(Guid callerId, CancellationToken ct = default);
	Task<OneOnOneDto> GetByIdAsync(Guid id, Guid callerId, CancellationToken ct = default);
	Task<OneOnOneDto> CreateAsync(CreateOneOnOneDto dto, Guid managerId, CancellationToken ct = default);
	Task<OneOnOneDto> UpdateAsync(Guid id, UpdateOneOnOneDto dto, Guid callerId, CancellationToken ct = default);
	Task DeleteAsync(Guid id, Guid callerId, CancellationToken ct = default);

	Task<OneOnOneDto> AddActionItemAsync(Guid oneOnOneId, CreateActionItemDto dto, Guid callerId, CancellationToken ct = default);
	Task<OneOnOneDto> ToggleActionItemAsync(Guid oneOnOneId, Guid itemId, Guid callerId, CancellationToken ct = default);
	Task<OneOnOneDto> RemoveActionItemAsync(Guid oneOnOneId, Guid itemId, Guid callerId, CancellationToken ct = default);

	/// <summary>Open action items assigned to me — feeds the My Inbox page.</summary>
	Task<IReadOnlyList<OneOnOneActionItemDto>> ListMyOpenActionItemsAsync(Guid callerId, CancellationToken ct = default);

	/// <summary>List 1:1s where the target collaborator is manager OR report. Admin/HR overview.</summary>
	Task<IReadOnlyList<OneOnOneDto>> ListForCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default);
}
