using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Notifications;

namespace Waao.Services.Abstractions.Dtos.Messaging;

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
}

public record EditMessageDto
{
	public string Body { get; init; } = string.Empty;
}

public record MarkReadDto
{
	public Guid LastReadMessageId { get; init; }
}

public record MessagePageDto
{
	public IReadOnlyList<MessageDto> Messages { get; init; } = [];
	public bool HasMore { get; init; }
}
