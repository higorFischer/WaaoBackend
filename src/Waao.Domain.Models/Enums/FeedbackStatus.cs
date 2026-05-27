namespace Waao.Domain.Models.Enums;

/// <summary>
/// Lifecycle of a feedback item from the admin's perspective. The submitter
/// never sees these — feedback is fire-and-forget (no reply flow).
/// </summary>
public enum FeedbackStatus
{
	New,
	Read,
	Resolved,
	Archived,
}
