namespace Waao.Domain.Models.Entities;

public class Challenge : Entity
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public int? SuggestedXp { get; set; }
	public int PassPercent { get; set; } = 70;
	public bool IsPublished { get; set; }

	public Guid CreatedById { get; set; }
	public virtual Collaborator CreatedBy { get; set; } = null!;

	public virtual ICollection<ChallengeQuestion> Questions { get; set; } = [];
	public virtual ICollection<ChallengeAttempt> Attempts { get; set; } = [];
}
