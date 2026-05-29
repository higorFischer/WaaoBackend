using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>No-op INotificationService for tests that don't care about notifications.</summary>
public sealed class NullNotificationService : INotificationService
{
	public static readonly NullNotificationService Instance = new();

	public Task CreateAsync(Guid recipientId, NotificationKind kind, string title, string body, string linkType, Guid linkId, Guid? actorId, CancellationToken ct = default)
		=> Task.CompletedTask;

	public Task CreateManyAsync(IReadOnlyCollection<Guid> recipientIds, NotificationKind kind, string title, string body, string linkType, Guid linkId, Guid? actorId, CancellationToken ct = default)
		=> Task.CompletedTask;

	public Task<NotificationListDto> ListAsync(Guid collaboratorId, bool unreadOnly = false, CancellationToken ct = default)
		=> Task.FromResult(new NotificationListDto());

	public Task MarkReadAsync(Guid notificationId, Guid collaboratorId, CancellationToken ct = default)
		=> Task.CompletedTask;

	public Task MarkAllReadAsync(Guid collaboratorId, CancellationToken ct = default)
		=> Task.CompletedTask;
}
