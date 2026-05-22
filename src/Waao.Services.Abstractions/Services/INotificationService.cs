using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Notifications;

namespace Waao.Services.Abstractions.Services;

public interface INotificationService
{
	/// <summary>
	/// Persists a Notification and broadcasts notificationReceived to the recipient's SignalR group user:{recipientId}.
	/// No-op if recipientId == actorId (never notify yourself).
	/// </summary>
	Task CreateAsync(
		Guid recipientId,
		NotificationKind kind,
		string title,
		string body,
		string linkType,
		Guid linkId,
		Guid? actorId,
		CancellationToken ct = default);

	/// <summary>
	/// Returns a list (cap 100, newest first) and the total unread count.
	/// </summary>
	Task<NotificationListDto> ListAsync(Guid collaboratorId, bool unreadOnly = false, CancellationToken ct = default);

	/// <summary>Marks a single notification read. Throws UnauthorizedAccessException if caller is not the recipient.</summary>
	Task MarkReadAsync(Guid notificationId, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Marks all of the collaborator's unread notifications as read.</summary>
	Task MarkAllReadAsync(Guid collaboratorId, CancellationToken ct = default);
}
