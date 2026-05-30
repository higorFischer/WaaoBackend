using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Services;
using Waao.Tests.Support;

namespace Waao.Tests.Messaging;

public class ChannelServiceTests
{
	private static ChannelService Build(out Waao.Infra.EF.WaaoDbContext db)
	{
		db = TestDb.New();
		return new ChannelService(db, NullNotificationService.Instance, NullMessageTextProtector.Instance);
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

	// ---- DM: find-or-create is idempotent ----

	[Fact]
	public async Task OpenDirectMessageAsync_CalledTwiceForSamePair_ReturnsSameChannelId()
	{
		var svc = Build(out var db);
		var alice = await SeedCollaborator(db, "Alice");
		var bob = await SeedCollaborator(db, "Bob");

		var first = await svc.OpenDirectMessageAsync(bob, alice);
		var second = await svc.OpenDirectMessageAsync(bob, alice);

		second.Id.Should().Be(first.Id);
	}

	[Fact]
	public async Task OpenDirectMessageAsync_FromOtherSide_ReturnsSameChannelId()
	{
		var svc = Build(out var db);
		var alice = await SeedCollaborator(db, "Alice");
		var bob = await SeedCollaborator(db, "Bob");

		var fromAlice = await svc.OpenDirectMessageAsync(bob, alice);
		var fromBob = await svc.OpenDirectMessageAsync(alice, bob);

		fromBob.Id.Should().Be(fromAlice.Id);
	}

	// ---- Create public channel ----

	[Fact]
	public async Task CreateChannelAsync_Public_CreatorAndInitialMembersJoined()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");
		var member1 = await SeedCollaborator(db, "Member1");
		var member2 = await SeedCollaborator(db, "Member2");

		var dto = new CreateChannelDto
		{
			Name = "General Chat",
			Kind = ChannelKind.Public,
			InitialMemberIds = [member1, member2],
		};

		var result = await svc.CreateChannelAsync(dto, creator);

		result.Kind.Should().Be(ChannelKind.Public);
		result.MemberCount.Should().Be(3); // creator + 2 initial

		var members = await db.ChannelMembers.Where(m => m.ChannelId == result.Id).ToListAsync();
		members.Should().HaveCount(3);
		members.Select(m => m.CollaboratorId).Should().Contain(creator);
		members.Select(m => m.CollaboratorId).Should().Contain(member1);
		members.Select(m => m.CollaboratorId).Should().Contain(member2);
	}

	[Fact]
	public async Task CreateChannelAsync_Private_CreatorAndInitialMembersJoined()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");
		var member = await SeedCollaborator(db, "Member");

		var dto = new CreateChannelDto
		{
			Name = "Private Ops",
			Kind = ChannelKind.Private,
			InitialMemberIds = [member],
		};

		var result = await svc.CreateChannelAsync(dto, creator);

		result.Kind.Should().Be(ChannelKind.Private);
		result.MemberCount.Should().Be(2);
	}

	// ---- Join: public OK, private → 403 ----

	[Fact]
	public async Task JoinAsync_PublicChannel_Succeeds()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");
		var joiner = await SeedCollaborator(db, "Joiner");

		var channel = await svc.CreateChannelAsync(new CreateChannelDto { Name = "Open", Kind = ChannelKind.Public, InitialMemberIds = [] }, creator);

		var result = await svc.JoinAsync(channel.Id, joiner);

		result.IsMember.Should().BeTrue();
		result.MemberCount.Should().Be(2);
	}

	[Fact]
	public async Task JoinAsync_PrivateChannel_ThrowsUnauthorizedAccessException()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");
		var outsider = await SeedCollaborator(db, "Outsider");

		var channel = await svc.CreateChannelAsync(new CreateChannelDto { Name = "Secret", Kind = ChannelKind.Private, InitialMemberIds = [] }, creator);

		var act = async () => await svc.JoinAsync(channel.Id, outsider);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	// ---- AddMember: actor must be a member ----

	[Fact]
	public async Task AddMemberAsync_ToPrivateChannel_AddedSuccessfully()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");
		var newMember = await SeedCollaborator(db, "NewMember");

		var channel = await svc.CreateChannelAsync(new CreateChannelDto { Name = "Ops", Kind = ChannelKind.Private, InitialMemberIds = [] }, creator);

		var result = await svc.AddMemberAsync(channel.Id, newMember, creator);

		result.MemberCount.Should().Be(2);

		var members = await db.ChannelMembers.Where(m => m.ChannelId == channel.Id).ToListAsync();
		members.Select(m => m.CollaboratorId).Should().Contain(newMember);
	}

	// ---- MarkRead ----

	[Fact]
	public async Task MarkReadAsync_SetsLastReadMessageId()
	{
		var svc = Build(out var db);
		var creator = await SeedCollaborator(db, "Creator");

		var channel = await svc.CreateChannelAsync(new CreateChannelDto { Name = "Chat", Kind = ChannelKind.Public, InitialMemberIds = [] }, creator);

		// Seed a message directly
		var msgId = Guid.CreateVersion7();
		db.Messages.Add(new Domain.Models.Entities.Messaging.Message
		{
			Id = msgId,
			ChannelId = channel.Id,
			AuthorId = creator,
			Body = "Hello",
			CreatedAt = DateTime.UtcNow,
		});
		await db.SaveChangesAsync();

		await svc.MarkReadAsync(channel.Id, new MarkReadDto { LastReadMessageId = msgId }, creator);

		var member = await db.ChannelMembers.FirstAsync(m => m.ChannelId == channel.Id && m.CollaboratorId == creator);
		member.LastReadMessageId.Should().Be(msgId);
	}
}
