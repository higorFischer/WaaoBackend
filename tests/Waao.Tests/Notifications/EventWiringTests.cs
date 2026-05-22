using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Services;
using Waao.Services.Video;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Notifications;

public class EventWiringTests
{
	private static (
		MessageService msgSvc,
		ChannelService chSvc,
		MeetingService meetingSvc,
		NotificationService notifSvc,
		CapturingBroadcaster broadcaster,
		Waao.Infra.EF.WaaoDbContext db) Build()
	{
		var db = TestDb.New();
		var broadcaster = new CapturingBroadcaster();
		var notifSvc = new NotificationService(db, broadcaster);
		var chSvc = new ChannelService(db, notifSvc);
		var msgSvc = new MessageService(db, notifSvc);
		var calSvc = new CalendarService(db);
		var meetingSvc = new MeetingService(db, calSvc, notifSvc, NullJaasTokenService.Instance, Options.Create(new JaasOptions()));
		return (msgSvc, chSvc, meetingSvc, notifSvc, broadcaster, db);
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

	// =====================================================================
	// PostMessage mention wiring
	// =====================================================================

	[Fact]
	public async Task PostMessage_MentionOfChannelMember_CreatesNotificationAndMentionRow()
	{
		var (msgSvc, chSvc, _, _, broadcaster, db) = Build();
		var author = await SeedCollaborator(db, "Author");
		var member = await SeedCollaborator(db, "Member");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [member] },
			author);

		var body = $"Hey @[Member]({member}) please look at this!";
		await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = body }, author);

		// MessageMention row persisted
		var mention = await db.MessageMentions.IgnoreQueryFilters().FirstOrDefaultAsync();
		mention.Should().NotBeNull();
		mention!.MentionedCollaboratorId.Should().Be(member);

		// Notification broadcast sent to member
		broadcaster.Calls.Should().ContainSingle(c => c.RecipientId == member);
	}

	[Fact]
	public async Task PostMessage_MentionOfNonMember_NoNotification()
	{
		var (msgSvc, chSvc, _, _, broadcaster, db) = Build();
		var author = await SeedCollaborator(db, "Author");
		var nonMember = await SeedCollaborator(db, "NonMember");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			author);

		var body = $"Hey @[NonMember]({nonMember}) please look!";
		await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = body }, author);

		broadcaster.Calls.Should().BeEmpty();
		var mentions = await db.MessageMentions.IgnoreQueryFilters().CountAsync();
		mentions.Should().Be(0);
	}

	[Fact]
	public async Task PostMessage_SelfMention_NoNotification()
	{
		var (msgSvc, chSvc, _, _, broadcaster, db) = Build();
		var author = await SeedCollaborator(db, "Author");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [] },
			author);

		var body = $"I @[Author]({author}) am mentioning myself.";
		await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = body }, author);

		broadcaster.Calls.Should().BeEmpty();
	}

	[Fact]
	public async Task PostMessage_ReturnedDto_IncludesMentions()
	{
		var (msgSvc, chSvc, _, _, _, db) = Build();
		var author = await SeedCollaborator(db, "Author");
		var member = await SeedCollaborator(db, "Member");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Chan", Kind = ChannelKind.Public, InitialMemberIds = [member] },
			author);

		var body = $"Hey @[Member]({member})!";
		var result = await msgSvc.PostMessageAsync(channel.Id, new PostMessageDto { Body = body }, author);

		result.Mentions.Should().ContainSingle(m => m.MentionedCollaboratorId == member);
	}

	// =====================================================================
	// AddMember → ChannelInvite
	// =====================================================================

	[Fact]
	public async Task AddMember_SendsChannelInviteNotification()
	{
		var (_, chSvc, _, _, broadcaster, db) = Build();
		var creator = await SeedCollaborator(db, "Creator");
		var newMember = await SeedCollaborator(db, "NewMember");

		var channel = await chSvc.CreateChannelAsync(
			new CreateChannelDto { Name = "Private", Kind = ChannelKind.Private, InitialMemberIds = [] },
			creator);

		broadcaster.Calls.Clear(); // ignore CreateAsync seeding notifications

		await chSvc.AddMemberAsync(channel.Id, newMember, creator);

		broadcaster.Calls.Should().ContainSingle(c =>
			c.RecipientId == newMember &&
			c.Dto.Kind == NotificationKind.ChannelInvite);
	}

	// =====================================================================
	// MeetingService event wiring
	// =====================================================================

	private static CreateMeetingDto BasicCreateDto(IReadOnlyList<Guid> attendeeIds) => new()
	{
		Title = "Team Sync",
		StartsAtUtc = DateTime.UtcNow.AddHours(1),
		EndsAtUtc = DateTime.UtcNow.AddHours(2),
		AttendeeCollaboratorIds = attendeeIds,
		AttendeeDepartmentIds = [],
		Agenda = [],
	};

	[Fact]
	public async Task MeetingCreate_SendsMeetingInviteToAttendeesNotOrganizer()
	{
		var (_, _, meetingSvc, _, broadcaster, db) = Build();
		var organizer = await SeedCollaborator(db, "Organizer");
		var attendee1 = await SeedCollaborator(db, "Attendee1");
		var attendee2 = await SeedCollaborator(db, "Attendee2");

		await meetingSvc.CreateAsync(BasicCreateDto([attendee1, attendee2]), organizer);

		broadcaster.Calls.Should().OnlyContain(c => c.Dto.Kind == NotificationKind.MeetingInvite);
		broadcaster.Calls.Should().Contain(c => c.RecipientId == attendee1);
		broadcaster.Calls.Should().Contain(c => c.RecipientId == attendee2);
		broadcaster.Calls.Should().NotContain(c => c.RecipientId == organizer);
	}

	[Fact]
	public async Task MeetingUpdate_SendsMeetingUpdatedToAttendeesNotActor()
	{
		var (_, _, meetingSvc, _, broadcaster, db) = Build();
		var organizer = await SeedCollaborator(db, "Organizer");
		var attendee = await SeedCollaborator(db, "Attendee");

		var created = await meetingSvc.CreateAsync(BasicCreateDto([attendee]), organizer);
		broadcaster.Calls.Clear();

		var updateDto = new UpdateMeetingDto
		{
			Title = "Updated Sync",
			StartsAtUtc = DateTime.UtcNow.AddHours(2),
			EndsAtUtc = DateTime.UtcNow.AddHours(3),
			AttendeeCollaboratorIds = [attendee],
			AttendeeDepartmentIds = [],
			Agenda = [],
		};
		await meetingSvc.UpdateAsync(created.Id, updateDto, organizer);

		broadcaster.Calls.Should().ContainSingle(c =>
			c.RecipientId == attendee &&
			c.Dto.Kind == NotificationKind.MeetingUpdated);
		broadcaster.Calls.Should().NotContain(c => c.RecipientId == organizer);
	}

	[Fact]
	public async Task MeetingCancel_SendsMeetingCancelledToAttendeesNotActor()
	{
		var (_, _, meetingSvc, _, broadcaster, db) = Build();
		var organizer = await SeedCollaborator(db, "Organizer");
		var attendee = await SeedCollaborator(db, "Attendee");

		var created = await meetingSvc.CreateAsync(BasicCreateDto([attendee]), organizer);
		broadcaster.Calls.Clear();

		await meetingSvc.CancelAsync(created.Id, organizer);

		broadcaster.Calls.Should().ContainSingle(c =>
			c.RecipientId == attendee &&
			c.Dto.Kind == NotificationKind.MeetingCancelled);
		broadcaster.Calls.Should().NotContain(c => c.RecipientId == organizer);
	}
}
