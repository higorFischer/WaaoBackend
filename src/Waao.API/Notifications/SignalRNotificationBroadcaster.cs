using Microsoft.AspNetCore.SignalR;
using Waao.API.Hubs;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Notifications;

/// <summary>
/// Concrete INotificationBroadcaster that routes to the per-user SignalR group
/// using the existing MessagingHub.
/// </summary>
public sealed class SignalRNotificationBroadcaster(IHubContext<MessagingHub> Hub) : INotificationBroadcaster
{
	public Task BroadcastAsync(Guid recipientId, NotificationDto dto, CancellationToken ct = default)
		=> Hub.Clients.Group(MessagingHub.UserGroupName(recipientId))
			.SendAsync("notificationReceived", dto, ct);
}
