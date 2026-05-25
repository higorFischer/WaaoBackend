using Waao.Services.Abstractions.Dtos.Messaging;

namespace Waao.Services.Abstractions.Services;

public interface IMessageService
{
	/// <summary>
	/// Returns a page of messages in the channel, ending before the <paramref name="before"/> cursor (null = newest).
	/// Limit is capped at 100. Caller must be a member (else UnauthorizedAccessException).
	/// Messages are returned oldest-first.
	/// </summary>
	Task<MessagePageDto> GetMessagesAsync(Guid channelId, Guid callerId, Guid? before, int limit, CancellationToken ct = default);

	/// <summary>
	/// Posts a message to the channel. Caller must be a member (else UnauthorizedAccessException).
	/// Returns the persisted MessageDto; the caller (controller) broadcasts it over SignalR.
	/// </summary>
	Task<MessageDto> PostMessageAsync(Guid channelId, PostMessageDto dto, Guid authorId, CancellationToken ct = default);

	/// <summary>Edits a message. Only the original author may edit; soft-deleted messages cannot be edited.</summary>
	Task<MessageDto> EditMessageAsync(Guid channelId, Guid messageId, EditMessageDto dto, Guid callerId, CancellationToken ct = default);
}
