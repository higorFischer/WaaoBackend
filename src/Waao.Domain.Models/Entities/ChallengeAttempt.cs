namespace Waao.Domain.Models.Entities;

public class ChallengeAttempt : Entity
{
	public Guid ChallengeId { get; set; }
	public virtual Challenge Challenge { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public DateTime StartedAt { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public int ScorePct { get; set; }
	public bool Passed { get; set; }

	public int? XpAwarded { get; set; }
	public DateTime? XpAwardedAt { get; set; }
	public Guid? XpAwardedByAdminId { get; set; }

	public virtual ICollection<ChallengeAttemptAnswer> Answers { get; set; } = [];
}
