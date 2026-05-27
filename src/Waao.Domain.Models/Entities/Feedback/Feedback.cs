using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Feedback;

/// <summary>
/// Internal employee feedback ("what's happening inside the company"). Always
/// attributed to the submitter; admin reads + triages but does not reply.
/// </summary>
public class Feedback : Entity
{
	public FeedbackCategory Category { get; set; } = FeedbackCategory.Other;
	public string Message { get; set; } = string.Empty;

	public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

	public Guid SubmittedById { get; set; }
	public virtual Collaborator SubmittedBy { get; set; } = null!;
}
