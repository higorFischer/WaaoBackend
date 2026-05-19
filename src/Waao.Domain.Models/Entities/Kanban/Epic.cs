namespace Waao.Domain.Models.Entities.Kanban;

public class Epic : Entity
{
	public Guid BoardId { get; set; }
	public virtual Board Board { get; set; } = null!;

	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#5BB3C4";
	public decimal Rank { get; set; }
	public bool IsArchived { get; set; }

	public virtual ICollection<Card> Cards { get; set; } = [];
}
