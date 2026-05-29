using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Messaging;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Services;
using Waao.Tests.Support;

namespace Waao.Tests.Messaging;

public class MessageServiceTests
{
	private static (MessageService msgSvc, ChannelService chSvc, Waao.Infra.EF.WaaoDbContext db)
		Build()
	{
		var db = TestDb.New();
		var chSvc = new ChannelService(db, NullNotificationService.Instance);
		var msgSvc = new MessageService(db, NullNotificationService.Instance, NullPushNotificationService.Instance, NullPresenceTracker.Instance, NullLogger<MessageService>.Instance);
		return (msgSvc, chSvc, db);
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

	// ---- Post: requires membership ----

	[Fact]
	public async Task PostMessageAsync_NonMember_ThrowsUnauthorizedAccessException()
	{
		var (msgSvc, chSvc, db) = Build();
		var owner = await SeedCollaborator(db, "Owner");
		var outsider = await SeedCollaborator(db, "Outsider");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			owner);

		var act = async () => await msgSvc.PostMessageAsync(
			channel.Id,
			new PostMessageDto { Body = "Hello" },
			outsider);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task PostMessageAsync_Member_PersistsAndReturnsMessageDto()
	{
		var (msgSvc, chSvc, db) = Build();
		var author = await SeedCollaborator(db, "Author");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chat", Kind = ChannelKind.Public, InitialMemberIds = [] },
			author);

		var result = await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = "Hello world" }, author);

		result.Body.Should().Be("Hello world");
		result.AuthorId.Should().Be(author);
		result.ChannelId.Should().Be(channel.Id);

		var saved = await db.Messages.FirstOrDefaultAsync(m => m.Id == result.Id);
		saved.Should().NotBeNull();
	}

	// ---- GetMessages: pagination ----

	[Fact]
	public async Task GetMessagesAsync_NonMember_ThrowsUnauthorizedAccessException()
	{
		var (msgSvc, chSvc, db) = Build();
		var owner = await SeedCollaborator(db, "Owner");
		var outsider = await SeedCollaborator(db, "Outsider");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			owner);

		var act = async () => await msgSvc.GetMessagesAsync(channel.Id, outsider, null, 50);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task GetMessagesAsync_LimitCappedAt100()
	{
		var (msgSvc, chSvc, db) = Build();
		var user = await SeedCollaborator(db, "User");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			user);

		// Seed 150 messages
		for (int i = 0; i < 150; i++)
		{
			db.Messages.Add(new Message
			{
				Id = Guid.CreateVersion7(),
				ChannelId = channel.Id,
				AuthorId = user,
				Body = $"Message {i}",
				CreatedAt = DateTime.UtcNow.AddSeconds(i),
			});
		}
		await db.SaveChangesAsync();

		var page = await msgSvc.GetMessagesAsync(channel.Id, user, null, 200);

		page.Messages.Count.Should().BeLessThanOrEqualTo(100);
		page.HasMore.Should().BeTrue();
	}

	[Fact]
	public async Task GetMessagesAsync_BeforeCursor_ReturnsPreviousMessages()
	{
		var (msgSvc, chSvc, db) = Build();
		var user = await SeedCollaborator(db, "User");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			user);

		// Seed 5 messages in order
		var messageIds = new List<Guid>();
		for (int i = 0; i < 5; i++)
		{
			var msg = new Message
			{
				Id = Guid.CreateVersion7(),
				ChannelId = channel.Id,
				AuthorId = user,
				Body = $"Message {i}",
				CreatedAt = DateTime.UtcNow.AddSeconds(i),
			};
			db.Messages.Add(msg);
			messageIds.Add(msg.Id);
		}
		await db.SaveChangesAsync();

		// Get 3 messages before message index 4 (the 5th message)
		var page = await msgSvc.GetMessagesAsync(channel.Id, user, messageIds[4], 10);

		// Should have messages 0-3 (4 messages before the cursor)
		page.Messages.Count.Should().Be(4);
		page.Messages.Should().AllSatisfy(m => m.Body.Should().NotBe("Message 4"));
		page.HasMore.Should().BeFalse();
	}

	[Fact]
	public async Task GetMessagesAsync_HasMore_WhenMoreOlderMessagesExist()
	{
		var (msgSvc, chSvc, db) = Build();
		var user = await SeedCollaborator(db, "User");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			user);

		// Seed 10 messages
		var messageIds = new List<Guid>();
		for (int i = 0; i < 10; i++)
		{
			var msg = new Message
			{
				Id = Guid.CreateVersion7(),
				ChannelId = channel.Id,
				AuthorId = user,
				Body = $"Message {i}",
				CreatedAt = DateTime.UtcNow.AddSeconds(i),
			};
			db.Messages.Add(msg);
			messageIds.Add(msg.Id);
		}
		await db.SaveChangesAsync();

		// Get 3 messages before the 8th message (index 7, 0-based)
		// Should have messages 4-6 (3 items with HasMore=true since messages 0-3 exist)
		var page = await msgSvc.GetMessagesAsync(channel.Id, user, messageIds[7], 3);

		page.Messages.Count.Should().Be(3);
		page.HasMore.Should().BeTrue();
	}

	// ---- Unread count integration with ChannelService.ListMyChannelsAsync ----

	[Fact]
	public async Task ListMyChannelsAsync_UnreadCount_CountsMessagesAfterLastRead()
	{
		var (msgSvc, chSvc, db) = Build();
		var user = await SeedCollaborator(db, "User");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chat", Kind = ChannelKind.Public, InitialMemberIds = [] },
			user);

		// Post 3 messages
		var msg1 = await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = "1" }, user);
		var msg2 = await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = "2" }, user);
		var msg3 = await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = "3" }, user);

		// Mark read at msg1 — unread should be msg2 + msg3 = 2
		await chSvc.MarkReadAsync(channel.Id, new MarkReadDto { LastReadMessageId = msg1.Id }, user);

		var channels = await chSvc.ListMyChannelsAsync(user);
		var ch = channels.First(c => c.Id == channel.Id);

		ch.UnreadCount.Should().Be(2);
	}
}
