using Waao.Services.Abstractions.Dtos.Kanban;

namespace Waao.Services.Abstractions.Services;

public interface IKanbanService
{
	Task<IReadOnlyList<MyKanbanCardDto>> ListMyCardsAsync(Guid currentCollaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<BoardSummaryDto>> ListBoardsAsync(Guid currentCollaboratorId, CancellationToken ct = default);
	Task<BoardDetailDto?> GetBoardAsync(string slug, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<BoardSummaryDto> CreateBoardAsync(CreateBoardDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<BoardSummaryDto> UpdateBoardAsync(Guid boardId, UpdateBoardDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteBoardAsync(Guid boardId, Guid currentCollaboratorId, CancellationToken ct = default);
	Task AddMemberAsync(Guid boardId, AddBoardMemberDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task RemoveMemberAsync(Guid boardId, Guid memberId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task<BoardColumnDto> CreateColumnAsync(Guid boardId, CreateColumnDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<BoardColumnDto> UpdateColumnAsync(Guid columnId, UpdateColumnDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteColumnAsync(Guid columnId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task<EpicDto> CreateEpicAsync(Guid boardId, CreateEpicDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteEpicAsync(Guid epicId, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardLabelDto> CreateLabelAsync(Guid boardId, CreateLabelDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteLabelAsync(Guid labelId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task<CardDetailDto> CreateCardAsync(CreateCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardDetailDto> UpdateCardAsync(Guid cardId, UpdateCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardDetailDto?> GetCardAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardMovedResultDto> MoveCardAsync(Guid cardId, MoveCardDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task ArchiveCardAsync(Guid cardId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task AddLabelToCardAsync(Guid cardId, Guid labelId, Guid currentCollaboratorId, CancellationToken ct = default);
	Task RemoveLabelFromCardAsync(Guid cardId, Guid labelId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task<CardCommentDto> CreateCommentAsync(Guid cardId, CreateCommentDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardCommentDto> UpdateCommentAsync(Guid commentId, UpdateCommentDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteCommentAsync(Guid commentId, Guid currentCollaboratorId, CancellationToken ct = default);

	Task<CardChecklistDto> CreateChecklistAsync(Guid cardId, CreateChecklistDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteChecklistAsync(Guid checklistId, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardChecklistItemDto> CreateChecklistItemAsync(Guid checklistId, CreateChecklistItemDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<CardChecklistItemDto> UpdateChecklistItemAsync(Guid itemId, UpdateChecklistItemDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task DeleteChecklistItemAsync(Guid itemId, Guid currentCollaboratorId, CancellationToken ct = default);
}
