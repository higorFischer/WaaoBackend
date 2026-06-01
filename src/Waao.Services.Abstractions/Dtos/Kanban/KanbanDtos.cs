using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Kanban;

public record BoardSummaryDto
{
	public Guid Id { get; init; }
	public string Slug { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public BoardVisibility Visibility { get; init; }
	public int? MinSeniorityOrder { get; init; }
	public Guid OwnerId { get; init; }
	public string? OwnerName { get; init; }
	public int CardCount { get; init; }
	public int MemberCount { get; init; }
	public bool IsArchived { get; init; }
	public Guid? ProjectId { get; init; }
	public string? ProjectTitle { get; init; }
	public string? ProjectColorHex { get; init; }
}

public record BoardDetailDto
{
	public Guid Id { get; init; }
	public string Slug { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public BoardVisibility Visibility { get; init; }
	public int? MinSeniorityOrder { get; init; }
	public Guid OwnerId { get; init; }
	public bool IsArchived { get; init; }
	public Guid? ProjectId { get; init; }
	public string? ProjectTitle { get; init; }
	public string? ProjectColorHex { get; init; }

	public IReadOnlyList<BoardColumnDto> Columns { get; init; } = [];
	public IReadOnlyList<EpicDto> Epics { get; init; } = [];
	public IReadOnlyList<CardLabelDto> Labels { get; init; } = [];
	public IReadOnlyList<BoardMemberDto> Members { get; init; } = [];
}

public record BoardMemberDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public BoardMemberRole Role { get; init; }
}

public record BoardColumnDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public decimal Rank { get; init; }
	public int? WipLimit { get; init; }
	public string? ColorHex { get; init; }
	public bool IsDone { get; init; }
	public IReadOnlyList<CardSummaryDto> Cards { get; init; } = [];
}

public record EpicDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#5BB3C4";
	public decimal Rank { get; init; }
	public bool IsArchived { get; init; }
	public int CardCount { get; init; }
	public int DoneCount { get; init; }
}

public record CardLabelDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string ColorHex { get; init; } = "#94a3b8";
}

public record CardSummaryDto
{
	public Guid Id { get; init; }
	public Guid ColumnId { get; init; }
	public Guid? EpicId { get; init; }
	public string? EpicTitle { get; init; }
	public string? EpicColorHex { get; init; }
	public string Title { get; init; } = string.Empty;
	public CardPriority Priority { get; init; }
	public decimal Rank { get; init; }
	public Guid? AssigneeId { get; init; }
	public string? AssigneeName { get; init; }
	public string? AssigneePhotoUrl { get; init; }
	public DateOnly? DueDate { get; init; }
	public int? StoryPoints { get; init; }
	public int CommentCount { get; init; }
	public int ChecklistDone { get; init; }
	public int ChecklistTotal { get; init; }
	public IReadOnlyList<CardLabelDto> Labels { get; init; } = [];
}

public record MyKanbanCardDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public CardPriority Priority { get; init; }
	public Guid BoardId { get; init; }
	public string BoardTitle { get; init; } = string.Empty;
	public string BoardSlug { get; init; } = string.Empty;
	public Guid ColumnId { get; init; }
	public string ColumnTitle { get; init; } = string.Empty;
	public string ColumnColorHex { get; init; } = "#94a3b8";
	public bool IsDoneColumn { get; init; }
	public Guid? EpicId { get; init; }
	public string? EpicTitle { get; init; }
	public string? EpicColorHex { get; init; }
	public DateOnly? DueDate { get; init; }
	public int? StoryPoints { get; init; }
	public int ChecklistDone { get; init; }
	public int ChecklistTotal { get; init; }
}

public record CardDetailDto : CardSummaryDto
{
	public string? Description { get; init; }
	public Guid ReporterId { get; init; }
	public string? ReporterName { get; init; }
	public DateTime? CompletedAt { get; init; }
	public bool IsArchived { get; init; }
	public IReadOnlyList<CardCommentDto> Comments { get; init; } = [];
	public IReadOnlyList<CardChecklistDto> Checklists { get; init; } = [];
	public IReadOnlyList<CardActivityDto> Activities { get; init; } = [];
}

public record CardCommentDto
{
	public Guid Id { get; init; }
	public Guid AuthorId { get; init; }
	public string AuthorName { get; init; } = string.Empty;
	public string? AuthorPhotoUrl { get; init; }
	public string Body { get; init; } = string.Empty;
	public DateTime CreatedAt { get; init; }
	public bool IsEdited { get; init; }
}

public record CardChecklistDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public decimal Rank { get; init; }
	public IReadOnlyList<CardChecklistItemDto> Items { get; init; } = [];
}

public record CardChecklistItemDto
{
	public Guid Id { get; init; }
	public string Text { get; init; } = string.Empty;
	public bool Done { get; init; }
	public decimal Rank { get; init; }
}

public record CardActivityDto
{
	public Guid Id { get; init; }
	public Guid ActorId { get; init; }
	public string ActorName { get; init; } = string.Empty;
	public CardActivityKind Kind { get; init; }
	public string? Detail { get; init; }
	public DateTime At { get; init; }
}

// ----- Create / update DTOs ---------------------------------------------

public record CreateBoardDto
{
	public string Slug { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public BoardVisibility Visibility { get; init; } = BoardVisibility.Team;
	public int? MinSeniorityOrder { get; init; }
	public bool SeedDefaultColumns { get; init; } = true;
	public Guid? ProjectId { get; init; }
}

public record UpdateBoardDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public BoardVisibility Visibility { get; init; }
	public int? MinSeniorityOrder { get; init; }
	public Guid? ProjectId { get; init; }
	public bool IsArchived { get; init; }
}

public record AddBoardMemberDto
{
	public Guid CollaboratorId { get; init; }
	public BoardMemberRole Role { get; init; } = BoardMemberRole.Editor;
}

public record CreateColumnDto
{
	public string Title { get; init; } = string.Empty;
	public Guid? AfterColumnId { get; init; }    // insert after this column for ranking
	public string? ColorHex { get; init; }
	public int? WipLimit { get; init; }
	public bool IsDone { get; init; }
}

public record UpdateColumnDto
{
	public string Title { get; init; } = string.Empty;
	public string? ColorHex { get; init; }
	public int? WipLimit { get; init; }
	public bool IsDone { get; init; }
}

public record CreateEpicDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#5BB3C4";
}

public record CreateLabelDto
{
	public string Name { get; init; } = string.Empty;
	public string ColorHex { get; init; } = "#94a3b8";
}

public record CreateCardDto
{
	public Guid ColumnId { get; init; }
	public Guid? EpicId { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public CardPriority Priority { get; init; } = CardPriority.Medium;
	public Guid? AssigneeId { get; init; }
	public DateOnly? DueDate { get; init; }
	public int? StoryPoints { get; init; }
	public IReadOnlyList<Guid>? LabelIds { get; init; }
}

public record UpdateCardDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public CardPriority Priority { get; init; }
	public Guid? EpicId { get; init; }
	public Guid? AssigneeId { get; init; }
	public DateOnly? DueDate { get; init; }
	public int? StoryPoints { get; init; }
}

public record MoveCardDto
{
	public Guid TargetColumnId { get; init; }
	public Guid? BeforeCardId { get; init; }   // place before this card
	public Guid? AfterCardId { get; init; }    // or after this card (one of the two; both null = end of column)
}

public record CreateCommentDto { public string Body { get; init; } = string.Empty; }
public record UpdateCommentDto { public string Body { get; init; } = string.Empty; }

public record CreateChecklistDto { public string Title { get; init; } = string.Empty; }
public record CreateChecklistItemDto { public string Text { get; init; } = string.Empty; }
public record UpdateChecklistItemDto { public string Text { get; init; } = string.Empty; public bool Done { get; init; } }

public record CardMovedResultDto
{
	public CardSummaryDto Card { get; init; } = new();
	public bool Completed { get; init; }
	public int XpAwarded { get; init; }
	public IReadOnlyList<BadgeDto> NewBadges { get; init; } = [];
}
