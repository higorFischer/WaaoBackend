using Waao.Services.Abstractions.Dtos.Calls;

namespace Waao.Services.Abstractions.Services;

public interface ICallChannelService
{
	Task<IReadOnlyList<CallChannelDto>> ListAsync(CancellationToken ct = default);
	Task<CallChannelDto> CreateAsync(CreateCallChannelDto dto, Guid creatorId, CancellationToken ct = default);
	Task<CallChannelDto> UpdateAsync(Guid id, UpdateCallChannelDto dto, CancellationToken ct = default);
	Task ArchiveAsync(Guid id, CancellationToken ct = default);
	Task<CallTokenDto> GetTokenAsync(Guid channelId, Guid callerId, CancellationToken ct = default);
}

/// <summary>
/// In-memory tracker of who is currently in which call channel. Singleton.
/// Updated by SignalR (CallsHub) — frontend reports join/leave; OnDisconnected cleans up.
/// </summary>
public interface ICallPresenceTracker
{
	void Join(Guid channelId, string connectionId, Guid collaboratorId, string fullName, string? photoUrl);
	IReadOnlyCollection<Guid> Leave(string connectionId);
	IReadOnlyList<CallParticipantDto> GetParticipants(Guid channelId);
	IReadOnlyDictionary<Guid, IReadOnlyList<CallParticipantDto>> SnapshotAll();
}
