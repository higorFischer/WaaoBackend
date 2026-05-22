using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Notifications;

public record NotificationDto
{
	public Guid Id { get; init; }
	public NotificationKind Kind { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Body { get; init; } = string.Empty;
	public string LinkType { get; init; } = string.Empty;
	public Guid LinkId { get; init; }
	public Guid? ActorId { get; init; }
	public string? ActorName { get; init; }
	public string? ActorPhotoUrl { get; init; }
	public bool IsRead { get; init; }
	public DateTime CreatedAtUtc { get; init; }
}

public record NotificationListDto
{
	public IReadOnlyList<NotificationDto> Items { get; init; } = [];
	public int UnreadCount { get; init; }
}

public record MessageMentionDto
{
	public Guid MentionedCollaboratorId { get; init; }
	public string MentionedCollaboratorName { get; init; } = string.Empty;
}
