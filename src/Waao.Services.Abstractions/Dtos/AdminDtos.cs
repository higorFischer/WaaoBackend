using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos;

// ----- Promote / role / department -----

public record PromoteCollaboratorDto
{
	public Guid? RoleId { get; init; }              // job role (e.g., "Senior Engineer")
	public Guid? DepartmentId { get; init; }        // optional department change
	public string Title { get; init; } = "Promotion";
	public string? Notes { get; init; }
	public DateOnly? EffectiveDate { get; init; }
}

public record SetRoleKindDto
{
	public CollaboratorRoleKind RoleKind { get; init; }
}

public record SetCollaboratorRoleDto
{
	public Guid? RoleId { get; init; }
	public Guid? DepartmentId { get; init; }
}

// ----- Job role catalog -----

public record JobRoleDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Track { get; init; }
	public int SeniorityOrder { get; init; }
	public string? Description { get; init; }
	public int CollaboratorCount { get; init; }
}

public record CreateJobRoleDto
{
	public string Title { get; init; } = string.Empty;
	public string? Track { get; init; }
	public int SeniorityOrder { get; init; } = 1;
	public string? Description { get; init; }
}

public record UpdateJobRoleDto : CreateJobRoleDto;

// ----- Department catalog -----

public record DepartmentDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#6366F1";
	public int CollaboratorCount { get; init; }
}

public record CreateDepartmentDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#6366F1";
}

public record UpdateDepartmentDto : CreateDepartmentDto;

// ----- Level catalog -----

public record LevelDefinitionDto
{
	public Guid Id { get; init; }
	public int Level { get; init; }
	public long XpThreshold { get; init; }
	public string Title { get; init; } = string.Empty;
	public string IconEmoji { get; init; } = "⭐";
	public string ColorHex { get; init; } = "#6366F1";
}

public record UpsertLevelDefinitionDto
{
	public int Level { get; init; }
	public long XpThreshold { get; init; }
	public string Title { get; init; } = string.Empty;
	public string IconEmoji { get; init; } = "⭐";
	public string ColorHex { get; init; } = "#6366F1";
}

// ----- XP Grant -----

public record GrantXpDto
{
	public int Amount { get; init; }
	public string Reason { get; init; } = string.Empty;
}
