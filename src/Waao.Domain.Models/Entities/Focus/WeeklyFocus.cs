namespace Waao.Domain.Models.Entities.Focus;

public class WeeklyFocus : Entity
{
	public int IsoYear { get; set; }
	public int IsoWeek { get; set; }

	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }

	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }

	public bool IsPublished { get; set; }
	public DateTime? PublishedAt { get; set; }

	public Guid OwnerId { get; set; }
	public string OwnerName { get; set; } = string.Empty;
	public virtual Collaborator Owner { get; set; } = null!;

	public virtual ICollection<WeeklyFocusGoal> Goals { get; set; } = [];
	public virtual ICollection<WeeklyFocusProject> Projects { get; set; } = [];
}
