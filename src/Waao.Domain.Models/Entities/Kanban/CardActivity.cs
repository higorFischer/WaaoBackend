using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Kanban;

public class CardActivity : Entity
{
	public Guid CardId { get; set; }
	public virtual Card Card { get; set; } = null!;

	public Guid ActorId { get; set; }
	public virtual Collaborator Actor { get; set; } = null!;

	public CardActivityKind Kind { get; set; }
	public string? Detail { get; set; }   // free-form: "Moved To Do → In Progress" / "Set due 2026-05-01"
}
