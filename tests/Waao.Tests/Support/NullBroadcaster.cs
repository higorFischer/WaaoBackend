using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>No-op INotificationBroadcaster for tests that don't care about notifications.</summary>
public sealed class NullBroadcaster : INotificationBroadcaster
{
	public static readonly NullBroadcaster Instance = new();

	public Task BroadcastAsync(Guid recipientId, NotificationDto dto, CancellationToken ct = default)
		=> Task.CompletedTask;
}
