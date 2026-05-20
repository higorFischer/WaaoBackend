namespace Waao.Domain.Models.Entities;

public class CourseCompletion : Entity
{
	public Guid CourseId { get; set; }
	public virtual Course Course { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public DateTime CompletedAt { get; set; }
	public string? Notes { get; set; }

	public int? XpAwarded { get; set; }
	public DateTime? XpAwardedAt { get; set; }
	public Guid? XpAwardedByAdminId { get; set; }
}
