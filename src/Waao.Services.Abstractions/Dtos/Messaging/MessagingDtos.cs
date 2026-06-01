using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Notifications;

namespace Waao.Services.Abstractions.Dtos.Messaging;

public record MessageAttachmentDto
{
	public Guid Id { get; init; }
	public MessageAttachmentKind Kind { get; init; }
	public string Url { get; init; } = string.Empty;
	public string Mime { get; init; } = string.Empty;
	public string OriginalName { get; init; } = string.Empty;
	public long SizeBytes { get; init; }
	public int? DurationSeconds { get; init; }
}

public record UploadedAttachmentDto
{
	public MessageAttachmentKind Kind { get; init; }
	/// <summary>Presigned (private) or public URL for immediate preview. Re-signed on read for private objects.</summary>
	public string Url { get; init; } = string.Empty;
	/// <summary>Private R2 object key — round-trips back on post so the server stores the key (not the
	/// expiring URL) and re-signs on every read. Null for legacy/public uploads.</summary>
	public string? StorageKey { get; init; }
	public string Mime { get; init; } = string.Empty;
	public string OriginalName { get; init; } = string.Empty;
	public long SizeBytes { get; init; }
	public int? DurationSeconds { get; init; }
}

public record ChannelDto
{
	public Guid Id { get; init; }
	public string? Name { get; init; }
	public string? Description { get; init; }
	public ChannelKind Kind { get; init; }
	public ChannelScope Scope { get; init; }
	public Guid? DepartmentId { get; init; }
	public Guid CreatedById { get; init; }
	public int MemberCount { get; init; }
	public bool IsMember { get; init; }
	public bool IsMuted { get; init; }
	public int UnreadCount { get; init; }
	public string? LastMessagePreview { get; init; }
	public DateTime? LastMessageAtUtc { get; init; }
	public ChannelMemberDto? OtherMember { get; init; }
}

public record ChannelMemberDto
{
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public string? CollaboratorPhotoUrl { get; init; }
	public DateTime JoinedAt { get; init; }
}

public record MessageDto
{
	public Guid Id { get; init; }
	public Guid ChannelId { get; init; }
	public Guid AuthorId { get; init; }
	public string AuthorName { get; init; } = string.Empty;
	public string? AuthorPhotoUrl { get; init; }
	public string Body { get; init; } = string.Empty;
	public DateTime CreatedAtUtc { get; init; }
	public DateTime? EditedAtUtc { get; init; }
	public Guid? ParentMessageId { get; init; }
	public ParentMessagePreviewDto? ParentPreview { get; init; }
	public IReadOnlyList<MessageMentionDto> Mentions { get; init; } = [];
	public IReadOnlyList<MessageAttachmentDto> Attachments { get; init; } = [];
	public IReadOnlyList<MessageReactionGroupDto> Reactions { get; init; } = [];
}

/// <summary>One emoji bucket on a message: who reacted with it, count, and whether the caller is one of them.</summary>
public record MessageReactionGroupDto
{
	public string Emoji { get; init; } = string.Empty;
	public int Count { get; init; }
	public bool Mine { get; init; }
	public IReadOnlyList<Guid> CollaboratorIds { get; init; } = [];
}

public record ReactionTogglePayloadDto
{
	public string Emoji { get; init; } = string.Empty;
}

/// <summary>Pushed to channel group when reactions on a message change.</summary>
public record MessageReactionUpdatedDto
{
	public Guid ChannelId { get; init; }
	public Guid MessageId { get; init; }
	public IReadOnlyList<MessageReactionGroupDto> Reactions { get; init; } = [];
}

public record RecentMessageDto : MessageDto
{
	public string ChannelName { get; init; } = string.Empty;
	public ChannelKind ChannelKind { get; init; }
}

public record ParentMessagePreviewDto
{
	public Guid Id { get; init; }
	public Guid AuthorId { get; init; }
	public string AuthorName { get; init; } = string.Empty;
	public string Body { get; init; } = string.Empty;
}

public record CreateChannelDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public ChannelKind Kind { get; init; }
	public IReadOnlyList<Guid> InitialMemberIds { get; init; } = [];
}

public record UpdateChannelDto
{
	public string? Name { get; init; }
	public string? Description { get; init; }
	public ChannelKind? Kind { get; init; }
}

public record PostMessageDto
{
	public string Body { get; init; } = string.Empty;
	public Guid? ParentMessageId { get; init; }
	public IReadOnlyList<UploadedAttachmentDto> Attachments { get; init; } = [];
}

public record EditMessageDto
{
	public string Body { get; init; } = string.Empty;
}

public record MarkReadDto
{
	public Guid LastReadMessageId { get; init; }
}

public record SetMutedDto
{
	public bool Muted { get; init; }
}

public record MessagePageDto
{
	public IReadOnlyList<MessageDto> Messages { get; init; } = [];
	public bool HasMore { get; init; }
}
