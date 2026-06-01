using System.Collections.Concurrent;
using Waao.Services.Abstractions.Dtos.Calls;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.Calls;

public sealed class CallPresenceTracker : ICallPresenceTracker
{
	private sealed record Entry(Guid ChannelId, Guid CollaboratorId, string FullName, string? PhotoUrl, DateTime JoinedAtUtc);

	// connectionId -> entry
	private readonly ConcurrentDictionary<string, Entry> _byConnection = new();

	public void Join(Guid channelId, string connectionId, Guid collaboratorId, string fullName, string? photoUrl)
	{
		// A single SignalR connection can only be in one call at a time; switching channels overwrites.
		_byConnection[connectionId] = new Entry(channelId, collaboratorId, fullName, photoUrl, DateTime.UtcNow);
	}

	public IReadOnlyCollection<Guid> Leave(string connectionId)
	{
		if (_byConnection.TryRemove(connectionId, out var entry))
			return new[] { entry.ChannelId };
		return Array.Empty<Guid>();
	}

	public IReadOnlyList<CallParticipantDto> GetParticipants(Guid channelId)
	{
		// Dedup by CollaboratorId — if a user has multiple tabs/devices joined, count once.
		return _byConnection.Values
			.Where(e => e.ChannelId == channelId)
			.GroupBy(e => e.CollaboratorId)
			.Select(g => g.OrderBy(e => e.JoinedAtUtc).First())
			.Select(e => new CallParticipantDto
			{
				CollaboratorId = e.CollaboratorId,
				FullName = e.FullName,
				PhotoUrl = e.PhotoUrl,
				JoinedAtUtc = e.JoinedAtUtc,
			})
			.ToList();
	}

	public IReadOnlyDictionary<Guid, IReadOnlyList<CallParticipantDto>> SnapshotAll()
	{
		return _byConnection.Values
			.GroupBy(e => e.ChannelId)
			.ToDictionary(
				g => g.Key,
				g => (IReadOnlyList<CallParticipantDto>)g
					.GroupBy(e => e.CollaboratorId)
					.Select(c => c.OrderBy(e => e.JoinedAtUtc).First())
					.Select(e => new CallParticipantDto
					{
						CollaboratorId = e.CollaboratorId,
						FullName = e.FullName,
						PhotoUrl = e.PhotoUrl,
						JoinedAtUtc = e.JoinedAtUtc,
					})
					.ToList());
	}
}
