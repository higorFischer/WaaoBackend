using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos;

public record CollaboratorDto
{
	public Guid Id { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public string? Cpf { get; init; }
	public DateOnly? Birthdate { get; init; }
	public DateOnly JoinDate { get; init; }
	public DateOnly? TerminationDate { get; init; }
	public string? PhotoUrl { get; init; }
	public string? Bio { get; init; }
	public CollaboratorStatus Status { get; init; }

	public Guid? DepartmentId { get; init; }
	public string? DepartmentName { get; init; }

	public Guid? RoleId { get; init; }
	public string? RoleTitle { get; init; }

	public Guid? ManagerId { get; init; }
	public string? ManagerName { get; init; }

	// Auth role (Collaborator / HR / Admin)
	public CollaboratorRoleKind RoleKind { get; init; }

	// Gamification snapshot
	public long TotalXp { get; init; }
	public int CurrentLevel { get; init; }
	public int CurrentStreakDays { get; init; }
	public int LongestStreakDays { get; init; }
	public int BadgeCount { get; init; }
}

public record CreateCollaboratorDto
{
	public string FullName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public string? Cpf { get; init; }
	public DateOnly? Birthdate { get; init; }
	public DateOnly JoinDate { get; init; }
	public string? PhotoUrl { get; init; }
	public string? Bio { get; init; }
	public Guid? DepartmentId { get; init; }
	public Guid? RoleId { get; init; }
	public Guid? ManagerId { get; init; }
}

public record UpdateCollaboratorDto
{
	public string FullName { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public DateOnly? Birthdate { get; init; }
	public string? PhotoUrl { get; init; }
	public string? Bio { get; init; }
	public Guid? DepartmentId { get; init; }
	public Guid? RoleId { get; init; }
	public Guid? ManagerId { get; init; }
	public CollaboratorStatus Status { get; init; }
	public DateOnly? TerminationDate { get; init; }
	public bool OptInLeaderboards { get; init; } = true;
}
