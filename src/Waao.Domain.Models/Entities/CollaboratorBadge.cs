namespace Waao.Domain.Models.Entities;

public class CollaboratorBadge : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public Guid BadgeId { get; set; }
	public virtual Badge Badge { get; set; } = null!;

	public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
	public string? Context { get; set; }       // why it was awarded, e.g., "5 years at WAAO"
}
