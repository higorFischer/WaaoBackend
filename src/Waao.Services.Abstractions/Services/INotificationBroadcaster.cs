using Waao.Services.Abstractions.Dtos.Notifications;

namespace Waao.Services.Abstractions.Services;

/// <summary>
/// Abstraction over the real-time transport so that NotificationService
/// does not depend on Waao.API (which would create a circular reference).
/// Waao.API provides a SignalR implementation.
/// </summary>
public interface INotificationBroadcaster
{
	/// <summary>
	/// Broadcasts a notification to the recipient's personal group "user:{recipientId}".
	/// </summary>
	Task BroadcastAsync(Guid recipientId, NotificationDto dto, CancellationToken ct = default);
}
