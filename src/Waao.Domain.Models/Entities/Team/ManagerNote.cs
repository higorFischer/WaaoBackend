namespace Waao.Domain.Models.Entities.Team;

public class ManagerNote : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator? Collaborator { get; set; }

	public Guid AuthorId { get; set; }
	public string AuthorName { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;
	public bool Pinned { get; set; }
}
