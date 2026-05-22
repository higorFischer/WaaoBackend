using Waao.Services.Abstractions.Dtos.Messaging;

namespace Waao.Services.Abstractions.Services;

public interface IChannelService
{
	/// <summary>Returns all channels (incl. DMs) the caller is a member of, with unread count and last-message preview.</summary>
	Task<IReadOnlyList<ChannelDto>> ListMyChannelsAsync(Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Returns public channels the caller is NOT a member of (browse-to-join list).</summary>
	Task<IReadOnlyList<ChannelDto>> ListPublicChannelsAsync(Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Creates a Public or Private channel. Creator + InitialMemberIds become members.</summary>
	Task<ChannelDto> CreateChannelAsync(CreateChannelDto dto, Guid creatorId, CancellationToken ct = default);

	/// <summary>Joins a public channel. Private channels → 403.</summary>
	Task<ChannelDto> JoinAsync(Guid channelId, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Leaves a channel.</summary>
	Task LeaveAsync(Guid channelId, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Adds a collaborator to a channel. Actor must be a member.</summary>
	Task<ChannelDto> AddMemberAsync(Guid channelId, Guid collaboratorId, Guid actorId, CancellationToken ct = default);

	/// <summary>Finds or creates the 1:1 DM channel between two collaborators. Never duplicated.</summary>
	Task<ChannelDto> OpenDirectMessageAsync(Guid otherCollaboratorId, Guid callerId, CancellationToken ct = default);

	/// <summary>Sets LastReadMessageId for the caller's membership in the channel.</summary>
	Task MarkReadAsync(Guid channelId, MarkReadDto dto, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Returns the members of a channel. Caller must be a member.</summary>
	Task<IReadOnlyList<ChannelMemberDto>> GetMembersAsync(Guid channelId, Guid callerId, CancellationToken ct = default);
}
