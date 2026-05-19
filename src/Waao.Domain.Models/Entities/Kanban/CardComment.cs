namespace Waao.Domain.Models.Entities.Kanban;

public class CardComment : Entity
{
	public Guid CardId { get; set; }
	public virtual Card Card { get; set; } = null!;

	public Guid AuthorId { get; set; }
	public virtual Collaborator Author { get; set; } = null!;

	public string Body { get; set; } = string.Empty;     // markdown
	public bool IsEdited { get; set; }
}
