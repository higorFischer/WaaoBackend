using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;
using Waao.Services.Push;
using WebPush;
using PushSubscriptionEntity = Waao.Domain.Models.Entities.Notifications.PushSubscription;

namespace Waao.Services.Services;

public sealed class PushNotificationService(
	WaaoDbContext Db,
	IOptions<VapidOptions> Vapid,
	ILogger<PushNotificationService> Logger) : IPushNotificationService
{
	public string PublicKey => Vapid.Value.PublicKey;

	// =====================================================================
	// SAVE (upsert by endpoint)
	// =====================================================================

	public async Task SaveSubscriptionAsync(Guid collaboratorId, SavePushSubscriptionDto dto, string? userAgent, CancellationToken ct = default)
	{
		var existing = await Db.PushSubscriptions
			.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint, ct);

		if (existing is not null)
		{
			existing.CollaboratorId = collaboratorId;
			existing.P256dh = dto.P256dh;
			existing.Auth = dto.Auth;
			existing.UserAgent = userAgent;
			existing.UpdatedAt = DateTime.UtcNow;
		}
		else
		{
			Db.PushSubscriptions.Add(new PushSubscriptionEntity
			{
				Id = Guid.CreateVersion7(),
				CollaboratorId = collaboratorId,
				Endpoint = dto.Endpoint,
				P256dh = dto.P256dh,
				Auth = dto.Auth,
				UserAgent = userAgent,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("Push subscription {Action} for collaborator {Collaborator} ({Host}).",
			existing is null ? "created" : "updated", collaboratorId, SafeHost(dto.Endpoint));
	}

	private static string SafeHost(string endpoint)
	{
		try { return new Uri(endpoint).Host; } catch { return "?"; }
	}

	// =====================================================================
	// REMOVE (soft delete)
	// =====================================================================

	public async Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default)
	{
		var now = DateTime.UtcNow;
		await Db.PushSubscriptions
			.Where(s => s.Endpoint == endpoint)
			.ExecuteUpdateAsync(
				s => s
					.SetProperty(x => x.IsDeleted, true)
					.SetProperty(x => x.DeletedAt, now)
					.SetProperty(x => x.UpdatedAt, now),
				ct);
	}

	// =====================================================================
	// SEND
	// =====================================================================

	public Task SendToCollaboratorAsync(Guid collaboratorId, string title, string body, string? url, CancellationToken ct = default)
		=> SendRichToCollaboratorAsync(collaboratorId, title, body, url, null, null, ct);

	public async Task SendRichToCollaboratorAsync(Guid collaboratorId, string title, string body, string? url, string? iconUrl, string? tag, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(Vapid.Value.PrivateKey))
		{
			Logger.LogDebug("Web Push skipped: no VAPID private key configured.");
			return;
		}

		var subscriptions = await Db.PushSubscriptions
			.Where(s => s.CollaboratorId == collaboratorId)
			.ToListAsync(ct);

		Logger.LogInformation("Web Push: sending to {Count} subscription(s) for collaborator {Collaborator}.", subscriptions.Count, collaboratorId);
		if (subscriptions.Count == 0)
			return;

		var vapidDetails = new VapidDetails(Vapid.Value.Subject, Vapid.Value.PublicKey, Vapid.Value.PrivateKey);
		var client = new WebPushClient();
		var payload = JsonSerializer.Serialize(new { title, body, url, icon = iconUrl, tag });

		foreach (var s in subscriptions)
		{
			try
			{
				var sub = new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth);
				await client.SendNotificationAsync(sub, payload, vapidDetails, ct);
				Logger.LogInformation("Web Push delivered to push service for {Host}.", SafeHost(s.Endpoint));
			}
			catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
			{
				// Endpoint is dead (unsubscribed / expired) — prune it so we stop trying.
				s.IsDeleted = true;
				s.DeletedAt = DateTime.UtcNow;
				s.UpdatedAt = DateTime.UtcNow;
				Logger.LogInformation("Pruned expired push subscription {Endpoint} (status {Status}).", s.Endpoint, ex.StatusCode);
			}
			catch (Exception ex)
			{
				// One bad endpoint must not break the whole fan-out.
				Logger.LogWarning(ex, "Failed to send Web Push to endpoint {Endpoint}.", s.Endpoint);
			}
		}

		if (Db.ChangeTracker.HasChanges())
			await Db.SaveChangesAsync(ct);
	}
}
