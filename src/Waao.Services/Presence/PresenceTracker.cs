using System.Collections.Concurrent;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Presence;

/// <summary>
/// Thread-safe, in-memory tracker of which channel each live SignalR connection is
/// actively viewing. Keyed by connectionId so reconnects and multiple tabs are isolated.
/// Singleton — safe because the API runs on a single Fly machine (no backplane).
/// </summary>
public sealed class PresenceTracker : IPresenceTracker
{
	private readonly ConcurrentDictionary<string, (Guid CollaboratorId, Guid ChannelId)> Connections = new();

	public void SetActive(string connectionId, Guid collaboratorId, Guid channelId)
		=> Connections[connectionId] = (collaboratorId, channelId);

	public void Remove(string connectionId)
		=> Connections.TryRemove(connectionId, out _);

	public bool IsActive(Guid collaboratorId, Guid channelId)
		=> Connections.Values.Any(v => v.CollaboratorId == collaboratorId && v.ChannelId == channelId);
}
