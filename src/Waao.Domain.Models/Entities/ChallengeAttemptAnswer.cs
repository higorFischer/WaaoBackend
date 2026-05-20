namespace Waao.Domain.Models.Entities;

public class ChallengeAttemptAnswer : Entity
{
	public Guid AttemptId { get; set; }
	public virtual ChallengeAttempt Attempt { get; set; } = null!;

	public Guid QuestionId { get; set; }
	public virtual ChallengeQuestion Question { get; set; } = null!;

	public char SelectedOption { get; set; }
	public bool IsCorrect { get; set; }
}
