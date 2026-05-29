using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>No-op IPushNotificationService for tests that don't care about Web Push.</summary>
public sealed class NullPushNotificationService : IPushNotificationService
{
	public static readonly NullPushNotificationService Instance = new();

	public string PublicKey => string.Empty;

	public Task SaveSubscriptionAsync(Guid collaboratorId, SavePushSubscriptionDto dto, string? userAgent, CancellationToken ct = default)
		=> Task.CompletedTask;

	public Task RemoveSubscriptionAsync(string endpoint, CancellationToken ct = default)
		=> Task.CompletedTask;

	public Task SendToCollaboratorAsync(Guid collaboratorId, string title, string body, string? url, CancellationToken ct = default)
		=> Task.CompletedTask;
}
