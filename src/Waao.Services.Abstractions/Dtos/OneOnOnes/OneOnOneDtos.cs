using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.OneOnOnes;

public record OneOnOneDto
{
	public Guid Id { get; init; }
	public Guid ManagerId { get; init; }
	public string ManagerName { get; init; } = string.Empty;
	public Guid ReportId { get; init; }
	public string ReportName { get; init; } = string.Empty;
	public DateOnly ScheduledDate { get; init; }
	public OneOnOneStatus Status { get; init; }
	public string? Agenda { get; init; }
	public string? Notes { get; init; }
	public DateTime? CompletedAtUtc { get; init; }
	public DateTime CreatedAtUtc { get; init; }
	public DateTime? UpdatedAtUtc { get; init; }
	public IReadOnlyList<OneOnOneActionItemDto> ActionItems { get; init; } = [];
}

public record OneOnOneActionItemDto
{
	public Guid Id { get; init; }
	public string Text { get; init; } = string.Empty;
	public bool IsDone { get; init; }
	public DateTime? DoneAtUtc { get; init; }
	public DateOnly? DueDate { get; init; }
	public Guid? AssignedToId { get; init; }
	public string? AssignedToName { get; init; }
	public int Position { get; init; }
}

public record CreateOneOnOneDto
{
	public Guid ReportId { get; init; }
	public DateOnly ScheduledDate { get; init; }
	public string? Agenda { get; init; }
}

public record UpdateOneOnOneDto
{
	public DateOnly ScheduledDate { get; init; }
	public string? Agenda { get; init; }
	public string? Notes { get; init; }
	public OneOnOneStatus Status { get; init; }
}

public record CreateActionItemDto
{
	public string Text { get; init; } = string.Empty;
	public DateOnly? DueDate { get; init; }
	public Guid? AssignedToId { get; init; }
}
