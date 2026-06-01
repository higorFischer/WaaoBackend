using Waao.Domain.Models.Entities.Allocation;

namespace Waao.Domain.Models.Entities.Focus;

public class WeeklyFocusProject : Entity
{
	public Guid WeeklyFocusId { get; set; }
	public virtual WeeklyFocus WeeklyFocus { get; set; } = null!;

	public Guid ProjectId { get; set; }
	public virtual Project Project { get; set; } = null!;

	public string ProjectTitle { get; set; } = string.Empty;
	public string ProjectColorHex { get; set; } = "#2A6B7E";

	public Guid? ParentProjectId { get; set; }

	public int Position { get; set; }
}
