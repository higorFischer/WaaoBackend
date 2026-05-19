using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Kanban;

public class Card : Entity
{
	public Guid BoardId { get; set; }            // denormalized for query convenience
	public virtual Board Board { get; set; } = null!;

	public Guid ColumnId { get; set; }
	public virtual BoardColumn Column { get; set; } = null!;

	public Guid? EpicId { get; set; }
	public virtual Epic? Epic { get; set; }

	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }    // markdown
	public CardPriority Priority { get; set; } = CardPriority.Medium;
	public decimal Rank { get; set; }            // sort within column

	public Guid? AssigneeId { get; set; }
	public virtual Collaborator? Assignee { get; set; }

	public Guid ReporterId { get; set; }
	public virtual Collaborator Reporter { get; set; } = null!;

	public DateOnly? DueDate { get; set; }
	public int? StoryPoints { get; set; }
	public bool IsArchived { get; set; }

	/// <summary>Set when the card first lands in a column where IsDone=true.</summary>
	public DateTime? CompletedAt { get; set; }

	public virtual ICollection<CardLabelMap> LabelMappings { get; set; } = [];
	public virtual ICollection<CardComment> Comments       { get; set; } = [];
	public virtual ICollection<CardChecklist> Checklists   { get; set; } = [];
	public virtual ICollection<CardActivity> Activities    { get; set; } = [];
}
