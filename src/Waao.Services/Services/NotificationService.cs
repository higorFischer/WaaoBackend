using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.Notifications;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class NotificationService(
	WaaoDbContext Db,
	INotificationBroadcaster Broadcaster,
	IPushNotificationService Push,
	ILogger<NotificationService> Logger) : INotificationService
{
	// =====================================================================
	// CREATE
	// =====================================================================

	public async Task CreateAsync(
		Guid recipientId,
		NotificationKind kind,
		string title,
		string body,
		string linkType,
		Guid linkId,
		Guid? actorId,
		CancellationToken ct = default)
	{
		// Never notify yourself
		if (actorId.HasValue && actorId.Value == recipientId)
			return;

		// Resolve actor name/photo for the broadcast DTO
		string? actorName = null;
		string? actorPhotoUrl = null;
		if (actorId.HasValue)
		{
			var actor = await Db.Collaborators.FindAsync([actorId.Value], ct);
			actorName = actor?.FullName;
			actorPhotoUrl = actor?.PhotoUrl;
		}

		var notification = new Notification
		{
			Id = Guid.CreateVersion7(),
			RecipientId = recipientId,
			Kind = kind,
			Title = title,
			Body = body,
			LinkType = linkType,
			LinkId = linkId,
			ActorId = actorId,
			IsRead = false,
			CreatedAt = DateTime.UtcNow,
		};

		Db.Notifications.Add(notification);
		await Db.SaveChangesAsync(ct);

		var dto = new NotificationDto
		{
			Id = notification.Id,
			Kind = notification.Kind,
			Title = notification.Title,
			Body = notification.Body,
			LinkType = notification.LinkType,
			LinkId = notification.LinkId,
			ActorId = notification.ActorId,
			ActorName = actorName,
			ActorPhotoUrl = actorPhotoUrl,
			IsRead = false,
			CreatedAtUtc = notification.CreatedAt,
		};

		await Broadcaster.BroadcastAsync(recipientId, dto, ct);

		await TrySendPushAsync(recipientId, title, body, linkType, linkId, ct);
	}

	// =====================================================================
	// CREATE MANY (single SaveChanges)
	// =====================================================================

	public async Task CreateManyAsync(
		IReadOnlyCollection<Guid> recipientIds,
		NotificationKind kind,
		string title,
		string body,
		string linkType,
		Guid linkId,
		Guid? actorId,
		CancellationToken ct = default)
	{
		var distinct = recipientIds
			.Where(id => !actorId.HasValue || id != actorId.Value)
			.Distinct()
			.ToList();
		if (distinct.Count == 0) return;

		string? actorName = null;
		string? actorPhotoUrl = null;
		if (actorId.HasValue)
		{
			var actor = await Db.Collaborators.AsNoTracking()
				.Where(c => c.Id == actorId.Value)
				.Select(c => new { c.FullName, c.PhotoUrl })
				.FirstOrDefaultAsync(ct);
			actorName = actor?.FullName;
			actorPhotoUrl = actor?.PhotoUrl;
		}

		var now = DateTime.UtcNow;
		var notifications = distinct.Select(rid => new Notification
		{
			Id = Guid.CreateVersion7(),
			RecipientId = rid,
			Kind = kind,
			Title = title,
			Body = body,
			LinkType = linkType,
			LinkId = linkId,
			ActorId = actorId,
			IsRead = false,
			CreatedAt = now,
		}).ToList();

		Db.Notifications.AddRange(notifications);
		await Db.SaveChangesAsync(ct);

		// Broadcast after persistence so the client receives a notification it
		// can also re-fetch from the API and find. Fire-and-forget the per-user
		// pushes in parallel so a slow SignalR send doesn't serialize the whole
		// fan-out.
		var broadcastTasks = notifications.Select(n => Broadcaster.BroadcastAsync(
			n.RecipientId,
			new NotificationDto
			{
				Id = n.Id,
				Kind = n.Kind,
				Title = n.Title,
				Body = n.Body,
				LinkType = n.LinkType,
				LinkId = n.LinkId,
				ActorId = n.ActorId,
				ActorName = actorName,
				ActorPhotoUrl = actorPhotoUrl,
				IsRead = false,
				CreatedAtUtc = n.CreatedAt,
			},
			ct));
		await Task.WhenAll(broadcastTasks);

		var pushTasks = notifications.Select(n =>
			TrySendPushAsync(n.RecipientId, n.Title, n.Body, n.LinkType, n.LinkId, ct));
		await Task.WhenAll(pushTasks);
	}

	// =====================================================================
	// LIST
	// =====================================================================

	public async Task<NotificationListDto> ListAsync(Guid collaboratorId, bool unreadOnly = false, CancellationToken ct = default)
	{
		var query = Db.Notifications
			.Include(n => n.Actor)
			.Where(n => n.RecipientId == collaboratorId);

		if (unreadOnly)
			query = query.Where(n => !n.IsRead);

		var items = await query
			.OrderByDescending(n => n.CreatedAt)
			.Take(100)
			.ToListAsync(ct);

		var unreadCount = await Db.Notifications
			.CountAsync(n => n.RecipientId == collaboratorId && !n.IsRead, ct);

		return new NotificationListDto
		{
			Items = items.Select(MapToDto).ToList(),
			UnreadCount = unreadCount,
		};
	}

	// =====================================================================
	// MARK READ
	// =====================================================================

	public async Task MarkReadAsync(Guid notificationId, Guid collaboratorId, CancellationToken ct = default)
	{
		var notification = await Db.Notifications
			.FirstOrDefaultAsync(n => n.Id == notificationId, ct)
			?? throw new KeyNotFoundException($"Notification {notificationId} not found.");

		if (notification.RecipientId != collaboratorId)
			throw new UnauthorizedAccessException($"Notification {notificationId} does not belong to caller.");

		notification.IsRead = true;
		notification.ReadAt = DateTime.UtcNow;
		notification.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// MARK ALL READ
	// =====================================================================

	public async Task MarkAllReadAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		// Single UPDATE — no entity hydration, no per-row tracking, one round trip.
		var now = DateTime.UtcNow;
		await Db.Notifications
			.Where(n => n.RecipientId == collaboratorId && !n.IsRead)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(n => n.IsRead, true)
					.SetProperty(n => n.ReadAt, now)
					.SetProperty(n => n.UpdatedAt, now),
				ct);
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private static NotificationDto MapToDto(Notification n) => new()
	{
		Id = n.Id,
		Kind = n.Kind,
		Title = n.Title,
		Body = n.Body,
		LinkType = n.LinkType,
		LinkId = n.LinkId,
		ActorId = n.ActorId,
		ActorName = n.Actor?.FullName,
		ActorPhotoUrl = n.Actor?.PhotoUrl,
		IsRead = n.IsRead,
		CreatedAtUtc = n.CreatedAt,
	};

	/// <summary>
	/// Best-effort background push delivery. Push is a "nice to have" on top of the
	/// persisted + broadcast notification, so a failure here must NEVER bubble up and
	/// break notification creation.
	/// </summary>
	private async Task TrySendPushAsync(Guid recipientId, string title, string body, string linkType, Guid linkId, CancellationToken ct)
	{
		try
		{
			await Push.SendToCollaboratorAsync(recipientId, title, body, BuildUrl(linkType, linkId), ct);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "Web Push delivery failed for recipient {RecipientId}.", recipientId);
		}
	}

	/// <summary>
	/// Maps a notification's link target to a frontend route the service worker opens on click.
	/// Mirrors the SPA routes (/messages, /meetings, /feature-requests).
	/// </summary>
	private static string BuildUrl(string linkType, Guid linkId) => linkType switch
	{
		"channel" => $"/messages?channel={linkId}",
		"meeting" => $"/meetings?meeting={linkId}",
		"feature-request" => "/feature-requests",
		"timeoff" => "/time-off",
		"kudos" => "/kudos",
		"badge" => "/profile",
		_ => "/",
	};
}
