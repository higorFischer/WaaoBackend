namespace Waao.Domain.Models.Entities;

public class Course : Entity
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? Provider { get; set; }
	public string? MaterialUrl { get; set; }
	public int? DurationMinutes { get; set; }
	public int? SuggestedXp { get; set; }
	public string Category { get; set; } = string.Empty;
	public bool IsPublished { get; set; }

	public Guid CreatedById { get; set; }
	public virtual Collaborator CreatedBy { get; set; } = null!;

	public virtual ICollection<CourseCompletion> Completions { get; set; } = [];
}
