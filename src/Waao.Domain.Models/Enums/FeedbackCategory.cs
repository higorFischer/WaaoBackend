namespace Waao.Domain.Models.Enums;

/// <summary>
/// Top-level grouping for an internal feedback submission. Lets the admin
/// triage culture/process concerns separately from product bugs.
/// </summary>
public enum FeedbackCategory
{
	Bug,
	Suggestion,
	Culture,
	Management,
	Other,
}
