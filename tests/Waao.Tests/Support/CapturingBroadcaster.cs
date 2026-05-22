using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>
/// A capturing INotificationBroadcaster stub for unit tests.
/// Records every BroadcastAsync call so tests can assert on it.
/// </summary>
public sealed class CapturingBroadcaster : INotificationBroadcaster
{
	private readonly List<(Guid RecipientId, NotificationDto Dto)> _calls = [];

	public IReadOnlyList<(Guid RecipientId, NotificationDto Dto)> Calls => _calls;

	public Task BroadcastAsync(Guid recipientId, NotificationDto dto, CancellationToken ct = default)
	{
		_calls.Add((recipientId, dto));
		return Task.CompletedTask;
	}
}
