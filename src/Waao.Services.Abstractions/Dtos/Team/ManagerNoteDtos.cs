namespace Waao.Services.Abstractions.Dtos.Team;

public record ManagerNoteDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public Guid AuthorId { get; init; }
	public string AuthorName { get; init; } = string.Empty;
	public string Body { get; init; } = string.Empty;
	public bool Pinned { get; init; }
	public DateTime CreatedAt { get; init; }
	public DateTime? UpdatedAt { get; init; }
}

public record CreateManagerNoteDto
{
	public string Body { get; init; } = string.Empty;
	public bool Pinned { get; init; }
}

public record UpdateManagerNoteDto
{
	public string Body { get; init; } = string.Empty;
	public bool Pinned { get; init; }
}
