using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Services;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Notifications;

public class NotificationServiceTests
{
	private static (NotificationService svc, Waao.Infra.EF.WaaoDbContext db, CapturingBroadcaster broadcaster)
		Build()
	{
		var db = TestDb.New();
		var broadcaster = new CapturingBroadcaster();
		var svc = new NotificationService(db, broadcaster);
		return (svc, db, broadcaster);
	}

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name = "User")
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLower()}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
			RoleKind = CollaboratorRoleKind.Collaborator,
		});
		await db.SaveChangesAsync();
		return id;
	}

	// ---- CreateAsync ----

	[Fact]
	public async Task CreateAsync_PersistsNotification()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");

		await svc.CreateAsync(recipient, NotificationKind.Mention, "Mentioned you", "message body", "channel", Guid.CreateVersion7(), actor);

		var saved = await db.Notifications.IgnoreQueryFilters().FirstOrDefaultAsync();
		saved.Should().NotBeNull();
		saved!.RecipientId.Should().Be(recipient);
		saved.Kind.Should().Be(NotificationKind.Mention);
		saved.IsRead.Should().BeFalse();
	}

	[Fact]
	public async Task CreateAsync_BroadcastsToUserGroup()
	{
		var (svc, db, broadcaster) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");

		await svc.CreateAsync(recipient, NotificationKind.Mention, "Mentioned you", "body", "channel", Guid.CreateVersion7(), actor);

		broadcaster.Calls.Should().ContainSingle(c => c.RecipientId == recipient);
	}

	[Fact]
	public async Task CreateAsync_NoOpWhenRecipientEqualsActor()
	{
		var (svc, db, broadcaster) = Build();
		var self = await SeedCollaborator(db, "Self");

		await svc.CreateAsync(self, NotificationKind.Mention, "You mentioned yourself", "body", "channel", Guid.CreateVersion7(), self);

		var count = await db.Notifications.IgnoreQueryFilters().CountAsync();
		count.Should().Be(0);
		broadcaster.Calls.Should().BeEmpty();
	}

	// ---- ListAsync ----

	[Fact]
	public async Task ListAsync_ReturnsAllNotifications_NewestFirst()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");
		var linkId = Guid.CreateVersion7();

		await svc.CreateAsync(recipient, NotificationKind.Mention, "First", "body1", "channel", linkId, actor);
		await svc.CreateAsync(recipient, NotificationKind.ChannelInvite, "Second", "body2", "channel", linkId, actor);

		var result = await svc.ListAsync(recipient, unreadOnly: false);

		result.Items.Should().HaveCount(2);
		result.UnreadCount.Should().Be(2);
		result.Items[0].Title.Should().Be("Second"); // newest first
	}

	[Fact]
	public async Task ListAsync_UnreadOnly_FiltersRead()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");
		var linkId = Guid.CreateVersion7();

		await svc.CreateAsync(recipient, NotificationKind.Mention, "Unread", "body", "channel", linkId, actor);
		await svc.CreateAsync(recipient, NotificationKind.ChannelInvite, "Also Unread", "body2", "channel", linkId, actor);

		// Mark "Unread" (the oldest, index 1 in newest-first list) as read
		var all = await svc.ListAsync(recipient, unreadOnly: false);
		// all.Items[0] = "Also Unread" (newest), all.Items[1] = "Unread" (oldest)
		await svc.MarkReadAsync(all.Items[1].Id, recipient);

		var unreadResult = await svc.ListAsync(recipient, unreadOnly: true);

		unreadResult.Items.Should().HaveCount(1);
		unreadResult.Items[0].Title.Should().Be("Also Unread");
		unreadResult.UnreadCount.Should().Be(1);
	}

	// ---- MarkReadAsync ----

	[Fact]
	public async Task MarkReadAsync_SetsIsReadAndReadAt()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");

		await svc.CreateAsync(recipient, NotificationKind.Mention, "Title", "body", "channel", Guid.CreateVersion7(), actor);

		var list = await svc.ListAsync(recipient, unreadOnly: false);
		await svc.MarkReadAsync(list.Items[0].Id, recipient);

		var notification = await db.Notifications.IgnoreQueryFilters().FirstAsync();
		notification.IsRead.Should().BeTrue();
		notification.ReadAt.Should().NotBeNull();
	}

	[Fact]
	public async Task MarkReadAsync_OtherUserNotification_ThrowsUnauthorizedAccessException()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");
		var stranger = await SeedCollaborator(db, "Stranger");

		await svc.CreateAsync(recipient, NotificationKind.Mention, "Title", "body", "channel", Guid.CreateVersion7(), actor);

		var list = await svc.ListAsync(recipient, unreadOnly: false);

		var act = async () => await svc.MarkReadAsync(list.Items[0].Id, stranger);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	// ---- MarkAllReadAsync ----

	[Fact]
	public async Task MarkAllReadAsync_MarksAllUnread()
	{
		var (svc, db, _) = Build();
		var recipient = await SeedCollaborator(db, "Recipient");
		var actor = await SeedCollaborator(db, "Actor");
		var linkId = Guid.CreateVersion7();

		await svc.CreateAsync(recipient, NotificationKind.Mention, "N1", "body", "channel", linkId, actor);
		await svc.CreateAsync(recipient, NotificationKind.ChannelInvite, "N2", "body", "channel", linkId, actor);

		await svc.MarkAllReadAsync(recipient);

		var unread = await svc.ListAsync(recipient, unreadOnly: true);
		unread.Items.Should().BeEmpty();
		unread.UnreadCount.Should().Be(0);
	}
}
