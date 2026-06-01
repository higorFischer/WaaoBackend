using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.TimeOff;

public record TimeOffRequestDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public TimeOffType Type { get; init; }
	public DateOnly StartDate { get; init; }
	public DateOnly EndDate { get; init; }
	public string? Reason { get; init; }
	public TimeOffStatus Status { get; init; }
	public Guid? ReviewedById { get; init; }
	public string? ReviewerName { get; init; }
	public DateTime? ReviewedAt { get; init; }
	public string? ReviewNote { get; init; }
	public DateTime CreatedAtUtc { get; init; }
	public int Days { get; init; }
}

public record CreateTimeOffDto
{
	public TimeOffType Type { get; init; }
	public DateOnly StartDate { get; init; }
	public DateOnly EndDate { get; init; }
	public string? Reason { get; init; }
}

public record ReviewTimeOffDto
{
	public string? Note { get; init; }
}

public record TimeOffAbsenceDto
{
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public TimeOffType Type { get; init; }
	public DateOnly StartDate { get; init; }
	public DateOnly EndDate { get; init; }
}

public record TimeOffBalanceDto
{
	public int EntitledDays { get; init; }
	public int TakenDays { get; init; }
	public int PendingDays { get; init; }
	public int RemainingDays { get; init; }
	public int Year { get; init; }
}

public record TimeOffOverlapDto
{
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public TimeOffType Type { get; init; }
	public DateOnly StartDate { get; init; }
	public DateOnly EndDate { get; init; }
	public TimeOffStatus Status { get; init; }
	public string? DepartmentName { get; init; }
}
