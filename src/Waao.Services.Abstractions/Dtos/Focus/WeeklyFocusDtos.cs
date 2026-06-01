namespace Waao.Services.Abstractions.Dtos.Focus;

public record WeeklyFocusDto
{
	public Guid Id { get; init; }
	public int IsoYear { get; init; }
	public int IsoWeek { get; init; }
	public DateOnly StartDate { get; init; }
	public DateOnly EndDate { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public bool IsPublished { get; init; }
	public DateTime? PublishedAt { get; init; }
	public Guid OwnerId { get; init; }
	public string OwnerName { get; init; } = string.Empty;
	public DateTime CreatedAtUtc { get; init; }
	public DateTime? UpdatedAtUtc { get; init; }
	public IReadOnlyList<WeeklyFocusGoalDto> Goals { get; init; } = [];
	public IReadOnlyList<WeeklyFocusProjectDto> Projects { get; init; } = [];
}

public record WeeklyFocusGoalDto
{
	public Guid Id { get; init; }
	public string Text { get; init; } = string.Empty;
	public bool IsDone { get; init; }
	public DateTime? DoneAt { get; init; }
	public int Position { get; init; }
}

public record WeeklyFocusProjectDto
{
	public Guid Id { get; init; }
	public Guid ProjectId { get; init; }
	public string ProjectTitle { get; init; } = string.Empty;
	public string ProjectColorHex { get; init; } = "#2A6B7E";
	public Guid? ParentProjectId { get; init; }
	public int Position { get; init; }
}

public record CreateWeeklyFocusDto
{
	public int IsoYear { get; init; }
	public int IsoWeek { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public IReadOnlyList<string> Goals { get; init; } = [];
	public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
	public bool Publish { get; init; }
}

public record UpdateWeeklyFocusDto
{
	public int IsoYear { get; init; }
	public int IsoWeek { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public IReadOnlyList<WeeklyFocusGoalInputDto> Goals { get; init; } = [];
	public IReadOnlyList<Guid> ProjectIds { get; init; } = [];
}

public record WeeklyFocusGoalInputDto
{
	public Guid? Id { get; init; }
	public string Text { get; init; } = string.Empty;
	public bool IsDone { get; init; }
}
