namespace Waao.Domain.Models.Entities.Focus;

public class WeeklyFocusGoal : Entity
{
	public Guid WeeklyFocusId { get; set; }
	public virtual WeeklyFocus WeeklyFocus { get; set; } = null!;

	public string Text { get; set; } = string.Empty;
	public bool IsDone { get; set; }
	public DateTime? DoneAt { get; set; }
	public int Position { get; set; }
}
