using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.FeatureRequests;

public record FeatureRequestDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public FeatureRequestStatus Status { get; init; }
	public string? AdminResponse { get; init; }
	public Guid SubmittedById { get; init; }
	public string SubmittedByName { get; init; } = string.Empty;
	public string? SubmittedByPhotoUrl { get; init; }
	public DateTime CreatedAt { get; init; }
	public int VoteCount { get; init; }
	public bool HasUpvoted { get; init; }
}

public record CreateFeatureRequestDto
{
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
}

public record UpdateFeatureRequestStatusDto
{
	public FeatureRequestStatus Status { get; init; }
	public string? AdminResponse { get; init; }
}
