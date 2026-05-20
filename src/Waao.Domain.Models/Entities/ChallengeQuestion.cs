namespace Waao.Domain.Models.Entities;

public class ChallengeQuestion : Entity
{
	public Guid ChallengeId { get; set; }
	public virtual Challenge Challenge { get; set; } = null!;

	public int Order { get; set; }
	public string Prompt { get; set; } = string.Empty;
	public string OptionA { get; set; } = string.Empty;
	public string OptionB { get; set; } = string.Empty;
	public string OptionC { get; set; } = string.Empty;
	public string OptionD { get; set; } = string.Empty;
	public char CorrectOption { get; set; }
}
