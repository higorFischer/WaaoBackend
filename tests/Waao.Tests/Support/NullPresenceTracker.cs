using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>No-op IPresenceTracker for tests — nobody is ever "active", so push is never suppressed.</summary>
public sealed class NullPresenceTracker : IPresenceTracker
{
	public static readonly NullPresenceTracker Instance = new();

	public void SetActive(string connectionId, Guid collaboratorId, Guid channelId) { }

	public void Remove(string connectionId) { }

	public bool IsActive(Guid collaboratorId, Guid channelId) => false;
}
