using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Notifications;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class NotificationService(
	WaaoDbContext Db,
	INotificationBroadcaster Broadcaster) : INotificationService
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
		var unread = await Db.Notifications
			.Where(n => n.RecipientId == collaboratorId && !n.IsRead)
			.ToListAsync(ct);

		var now = DateTime.UtcNow;
		foreach (var n in unread)
		{
			n.IsRead = true;
			n.ReadAt = now;
			n.UpdatedAt = now;
		}

		await Db.SaveChangesAsync(ct);
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
}
