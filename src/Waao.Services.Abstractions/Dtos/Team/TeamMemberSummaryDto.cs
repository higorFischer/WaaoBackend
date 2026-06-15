using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Team;

public record TeamMemberSummaryDto
{
	public Guid Id { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public string? RoleTitle { get; init; }
	public CollaboratorStatus Status { get; init; }
	public int AllocationCount { get; init; }
	public int SkillCount { get; init; }
	public DateOnly? LastOneOnOneDate { get; init; }
}
