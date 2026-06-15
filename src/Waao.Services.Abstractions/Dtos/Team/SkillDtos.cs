using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Team;

public record SkillDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Category { get; init; }
	public bool IsArchived { get; init; }
}

public record CreateSkillDto
{
	public string Name { get; init; } = string.Empty;
	public string? Category { get; init; }
}

public record UpdateSkillDto
{
	public string Name { get; init; } = string.Empty;
	public string? Category { get; init; }
	public bool IsArchived { get; init; }
}

public record CollaboratorSkillDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public Guid SkillId { get; init; }
	public string SkillName { get; init; } = string.Empty;
	public string? SkillCategory { get; init; }
	public SkillLevel Level { get; init; }
	public string? Note { get; init; }
	public Guid AssessedById { get; init; }
	public DateTime AssessedAt { get; init; }
}

public record UpsertCollaboratorSkillDto
{
	public SkillLevel Level { get; init; } = SkillLevel.Competent;
	public string? Note { get; init; }
}
