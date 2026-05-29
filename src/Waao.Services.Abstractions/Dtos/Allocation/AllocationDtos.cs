namespace Waao.Services.Abstractions.Dtos.Allocation;

public record CollaboratorChipDto
{
	public Guid Id { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public string? RoleTitle { get; init; }
	public string? DepartmentName { get; init; }
}

public record AllocationDto
{
	public Guid Id { get; init; }
	public Guid ProjectId { get; init; }
	public string? Note { get; init; }
	public int Position { get; init; }
	public DateTime AllocatedAt { get; init; }
	public CollaboratorChipDto Collaborator { get; init; } = new();
}

public record ProjectWithAllocationsDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public int Position { get; init; }
	public double PositionX { get; init; }
	public double PositionY { get; init; }
	public Guid? ParentProjectId { get; init; }
	public IReadOnlyList<AllocationDto> Allocations { get; init; } = [];
}

public record AllocationBoardDto
{
	public IReadOnlyList<ProjectWithAllocationsDto> Projects { get; init; } = [];
	public IReadOnlyList<CollaboratorChipDto> Collaborators { get; init; } = [];
	public IReadOnlyList<ProjectConnectionDto> Connections { get; init; } = [];
}

public record ProjectConnectionDto
{
	public Guid Id { get; init; }
	public Guid SourceProjectId { get; init; }
	public Guid TargetProjectId { get; init; }
	public string? Label { get; init; }
}

public record CreateConnectionDto
{
	public Guid SourceProjectId { get; init; }
	public Guid TargetProjectId { get; init; }
	public string? Label { get; init; }
}

public record UpdatePositionDto
{
	public double X { get; init; }
	public double Y { get; init; }
}

public record CreateProjectDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? ColorHex { get; init; }
}

public record UpdateProjectDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
}

public record ReorderProjectsDto
{
	public IReadOnlyList<Guid> OrderedIds { get; init; } = [];
}

public record CreateAllocationDto
{
	public Guid ProjectId { get; init; }
	public Guid CollaboratorId { get; init; }
	public string? Note { get; init; }
}

public record MoveAllocationDto
{
	public Guid ProjectId { get; init; }
	public int Position { get; init; }
}

public record UpdateNoteDto
{
	public string? Note { get; init; }
}

public record SetParentDto
{
	public Guid? ParentProjectId { get; init; }
	public double X { get; init; }
	public double Y { get; init; }
}
