namespace Waao.Domain.Models.Entities.Kanban;

public class CardChecklist : Entity
{
	public Guid CardId { get; set; }
	public virtual Card Card { get; set; } = null!;

	public string Title { get; set; } = string.Empty;
	public decimal Rank { get; set; }

	public virtual ICollection<CardChecklistItem> Items { get; set; } = [];
}

public class CardChecklistItem : Entity
{
	public Guid ChecklistId { get; set; }
	public virtual CardChecklist Checklist { get; set; } = null!;

	public string Text { get; set; } = string.Empty;
	public bool Done { get; set; }
	public decimal Rank { get; set; }
}
