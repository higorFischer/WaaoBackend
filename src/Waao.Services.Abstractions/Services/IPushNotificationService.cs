using Waao.Services.Abstractions.Dtos.Notifications;

namespace Waao.Services.Abstractions.Services;

public interface IPushNotificationService
{
	/// <summary>The VAPID public key clients use to subscribe.</summary>
	string PublicKey { get; }

	/// <summary>
	/// Upserts a Web Push subscription by endpoint. If a non-deleted row with the
	/// endpoint exists, its keys/collaborator/user agent are refreshed; else a new row is inserted.
	/// </summary>
	Task SaveSubscriptionAsync(Guid collaboratorId, SavePushSubscriptionDto dto, string? userAgent, CancellationToken ct = default);

	/// <summary>Soft-deletes the subscription(s) with the given endpoint.</summary>
	Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default);

	/// <summary>
	/// Sends a Web Push to every live subscription of the collaborator. No-op when no VAPID
	/// private key is configured. Expired endpoints (404/410) are pruned automatically.
	/// </summary>
	Task SendToCollaboratorAsync(Guid collaboratorId, string title, string body, string? url, CancellationToken ct = default);

	/// <summary>
	/// Rich variant. <paramref name="iconUrl"/> shows next to the notification (sender avatar
	/// works great for chat). <paramref name="tag"/> deduplicates: a new push with the same tag
	/// replaces the previous one, so a channel doesn't pile up six toasts.
	/// </summary>
	Task SendRichToCollaboratorAsync(Guid collaboratorId, string title, string body, string? url, string? iconUrl, string? tag, CancellationToken ct = default);
}
