using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Feedback;

public record PeerFeedbackDto
{
	public Guid Id { get; init; }
	/// <summary>Null when IsAnonymous and the caller isn't staff.</summary>
	public Guid? GiverId { get; init; }
	public string? GiverName { get; init; }
	public string? GiverPhotoUrl { get; init; }
	public Guid RecipientId { get; init; }
	public string RecipientName { get; init; } = string.Empty;
	public PeerFeedbackCategory Category { get; init; }
	public string Message { get; init; } = string.Empty;
	public bool IsAnonymous { get; init; }
	public bool Acknowledged { get; init; }
	public DateTime? AcknowledgedAtUtc { get; init; }
	public DateTime CreatedAtUtc { get; init; }
}

public record GivePeerFeedbackDto
{
	public Guid RecipientId { get; init; }
	public PeerFeedbackCategory Category { get; init; }
	public string Message { get; init; } = string.Empty;
	public bool IsAnonymous { get; init; }
}
