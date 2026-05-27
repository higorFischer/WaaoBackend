using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Feedback;

public record FeedbackDto
{
	public Guid Id { get; init; }
	public FeedbackCategory Category { get; init; }
	public string Message { get; init; } = string.Empty;
	public FeedbackStatus Status { get; init; }
	public Guid SubmittedById { get; init; }
	public string SubmittedByName { get; init; } = string.Empty;
	public string? SubmittedByPhotoUrl { get; init; }
	public DateTime CreatedAt { get; init; }
}

public record CreateFeedbackDto
{
	public FeedbackCategory Category { get; init; } = FeedbackCategory.Other;
	public string Message { get; init; } = string.Empty;
}

public record UpdateFeedbackStatusDto
{
	public FeedbackStatus Status { get; init; }
}
