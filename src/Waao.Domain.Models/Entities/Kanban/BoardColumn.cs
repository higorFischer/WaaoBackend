namespace Waao.Domain.Models.Entities.Kanban;

public class BoardColumn : Entity
{
	public Guid BoardId { get; set; }
	public virtual Board Board { get; set; } = null!;

	public string Title { get; set; } = string.Empty;
	public decimal Rank { get; set; }
	public int? WipLimit { get; set; }
	public string? ColorHex { get; set; }

	/// <summary>Columns flagged as "done" trigger card-completion XP awards on entry.</summary>
	public bool IsDone { get; set; }

	public virtual ICollection<Card> Cards { get; set; } = [];
}
