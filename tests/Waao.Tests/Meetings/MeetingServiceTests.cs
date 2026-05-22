using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Services;
using Waao.Tests.Support;

namespace Waao.Tests.Meetings;

public class MeetingServiceTests
{
	private static (MeetingService svc, Waao.Infra.EF.WaaoDbContext db)
		Build()
	{
		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var svc = new MeetingService(db, calSvc);
		return (svc, db);
	}

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name = "Test User", CollaboratorRoleKind role = CollaboratorRoleKind.Collaborator)
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLowerInvariant()}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
			RoleKind = role,
		});
		await db.SaveChangesAsync();
		return id;
	}

	private static async Task<Guid> SeedDepartmentWithMember(Waao.Infra.EF.WaaoDbContext db, Guid memberId)
	{
		var deptId = Guid.CreateVersion7();
		db.Departments.Add(new Department
		{
			Id = deptId,
			Name = "Engineering",
		});
		await db.SaveChangesAsync();

		var collab = await db.Collaborators.FindAsync(memberId);
		collab!.DepartmentId = deptId;
		await db.SaveChangesAsync();

		return deptId;
	}

	private static CreateMeetingDto BasicCreateDto(IReadOnlyList<Guid> attendeeIds) => new()
	{
		Title = "Team Sync",
		StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
		EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
		AttendeeCollaboratorIds = attendeeIds,
		AttendeeDepartmentIds = [],
		Agenda = [],
	};

	// ---- Create: backing event + meeting created; organizer auto-added as Going ----

	[Fact]
	public async Task CreateAsync_CreatesMeetingAndBackingEvent_OrganizerIsGoingAttendee()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var dto = BasicCreateDto([attendeeId]);
		var result = await svc.CreateAsync(dto, organizerId);

		// Meeting created
		result.Should().NotBeNull();
		result.Title.Should().Be("Team Sync");
		result.OrganizerId.Should().Be(organizerId);

		// Backing CalendarEvent exists
		result.CalendarEventId.Should().NotBeEmpty();
		var evt = await db.CalendarEvents.FindAsync(result.CalendarEventId);
		evt.Should().NotBeNull();
		evt!.Title.Should().Be("Team Sync");

		// Organizer is a Going attendee
		result.Attendees.Should().Contain(a => a.CollaboratorId == organizerId && a.Rsvp == MeetingRsvp.Going);

		// Invited attendee has NoResponse
		result.Attendees.Should().Contain(a => a.CollaboratorId == attendeeId && a.Rsvp == MeetingRsvp.NoResponse);
	}

	// ---- Department invite expands to member rows tagged with InvitedViaDepartmentId ----

	[Fact]
	public async Task CreateAsync_DepartmentInvite_ExpandsToMembersWithDepartmentTag()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var memberId = await SeedCollaborator(db, "DeptMember");
		var deptId = await SeedDepartmentWithMember(db, memberId);

		var dto = new CreateMeetingDto
		{
			Title = "All Hands",
			StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
			EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
			AttendeeCollaboratorIds = [],
			AttendeeDepartmentIds = [deptId],
			Agenda = [],
		};

		var result = await svc.CreateAsync(dto, organizerId);

		// Department member should be added with InvitedViaDepartmentId set
		var memberAttendee = result.Attendees.FirstOrDefault(a => a.CollaboratorId == memberId);
		memberAttendee.Should().NotBeNull();
		memberAttendee!.InvitedViaDepartmentId.Should().Be(deptId);
	}

	// ---- Double-invite de-dupe: one row, InvitedViaDepartmentId null ----

	[Fact]
	public async Task CreateAsync_CollaboratorInvitedBothWays_GetsOneRowWithNullDeptId()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var memberId = await SeedCollaborator(db, "BothInvited");
		var deptId = await SeedDepartmentWithMember(db, memberId);

		var dto = new CreateMeetingDto
		{
			Title = "Both Ways",
			StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
			EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
			AttendeeCollaboratorIds = [memberId], // individual invite
			AttendeeDepartmentIds = [deptId],     // also via department
			Agenda = [],
		};

		var result = await svc.CreateAsync(dto, organizerId);

		// Should have only ONE row for memberId, with InvitedViaDepartmentId = null (individual wins)
		var memberAttendees = result.Attendees.Where(a => a.CollaboratorId == memberId).ToList();
		memberAttendees.Should().HaveCount(1);
		memberAttendees[0].InvitedViaDepartmentId.Should().BeNull();
	}

	// ---- GetAsync: visible to organizer/attendee/Admin; 404 for unrelated caller ----

	[Fact]
	public async Task GetAsync_UnrelatedCaller_ThrowsKeyNotFound()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var unrelatedId = await SeedCollaborator(db, "Unrelated");

		var dto = BasicCreateDto([attendeeId]);
		var created = await svc.CreateAsync(dto, organizerId);

		var act = async () => await svc.GetAsync(created.Id, unrelatedId);
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}

	[Fact]
	public async Task GetAsync_OrganizerCanRead()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var dto = BasicCreateDto([attendeeId]);
		var created = await svc.CreateAsync(dto, organizerId);

		var result = await svc.GetAsync(created.Id, organizerId);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task GetAsync_AdminCanRead()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var adminId = await SeedCollaborator(db, "Admin", CollaboratorRoleKind.Admin);

		var dto = BasicCreateDto([attendeeId]);
		var created = await svc.CreateAsync(dto, organizerId);

		var result = await svc.GetAsync(created.Id, adminId);
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task GetAsync_MyRsvp_ReflectsCallerRsvp()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var dto = BasicCreateDto([attendeeId]);
		var created = await svc.CreateAsync(dto, organizerId);

		// Organizer is Going; attendee is NoResponse
		var organizerView = await svc.GetAsync(created.Id, organizerId);
		organizerView.MyRsvp.Should().Be(MeetingRsvp.Going);

		var attendeeView = await svc.GetAsync(created.Id, attendeeId);
		attendeeView.MyRsvp.Should().Be(MeetingRsvp.NoResponse);
	}

	// ---- Update: added attendees start NoResponse; removed soft-deleted; survivors keep RSVP; agenda replaced ----

	[Fact]
	public async Task UpdateAsync_AddsAndRemovesAttendees_KeepsSurvivorRsvp()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var keepId = await SeedCollaborator(db, "Keep");
		var removeId = await SeedCollaborator(db, "Remove");
		var newId = await SeedCollaborator(db, "New");

		var created = await svc.CreateAsync(BasicCreateDto([keepId, removeId]), organizerId);

		// Organizer sets keepId's RSVP to Going
		await svc.SetRsvpAsync(created.Id, new SetRsvpDto { Rsvp = MeetingRsvp.Going }, keepId);

		var updateDto = new UpdateMeetingDto
		{
			Title = "Updated Sync",
			StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
			EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
			AttendeeCollaboratorIds = [keepId, newId], // remove removeId
			AttendeeDepartmentIds = [],
			Agenda = [new CreateAgendaItemDto { Title = "New Agenda" }],
		};

		var updated = await svc.UpdateAsync(created.Id, updateDto, organizerId);

		// removeId no longer an attendee
		updated.Attendees.Should().NotContain(a => a.CollaboratorId == removeId);

		// keepId still Going (RSVP preserved)
		updated.Attendees.Should().Contain(a => a.CollaboratorId == keepId && a.Rsvp == MeetingRsvp.Going);

		// newId added with NoResponse
		updated.Attendees.Should().Contain(a => a.CollaboratorId == newId && a.Rsvp == MeetingRsvp.NoResponse);

		// Agenda replaced
		updated.Agenda.Should().HaveCount(1);
		updated.Agenda[0].Title.Should().Be("New Agenda");
	}

	// ---- SetRsvp: attendee can set; non-attendee -> 403; NoResponse rejected ----

	[Fact]
	public async Task SetRsvpAsync_NonAttendee_ThrowsUnauthorized()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var outsiderId = await SeedCollaborator(db, "Outsider");

		var created = await svc.CreateAsync(BasicCreateDto([attendeeId]), organizerId);

		var act = async () => await svc.SetRsvpAsync(created.Id, new SetRsvpDto { Rsvp = MeetingRsvp.Going }, outsiderId);
		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task SetRsvpAsync_ValidRsvp_UpdatesAndStampsRespondedAt()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto([attendeeId]), organizerId);

		var result = await svc.SetRsvpAsync(created.Id, new SetRsvpDto { Rsvp = MeetingRsvp.Going }, attendeeId);

		var attendee = result.Attendees.First(a => a.CollaboratorId == attendeeId);
		attendee.Rsvp.Should().Be(MeetingRsvp.Going);
		attendee.RespondedAt.Should().NotBeNull();
	}

	// ---- Cancel: organizer/Admin can; non-organizer -> 403; all entities soft-deleted ----

	[Fact]
	public async Task CancelAsync_NonOrganizer_ThrowsUnauthorized()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto([attendeeId]), organizerId);

		var act = async () => await svc.CancelAsync(created.Id, attendeeId);
		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task CancelAsync_Organizer_SoftDeletesMeetingAttendeeAgendaEvent()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(new CreateMeetingDto
		{
			Title = "To Cancel",
			StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
			EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
			AttendeeCollaboratorIds = [attendeeId],
			AttendeeDepartmentIds = [],
			Agenda = [new CreateAgendaItemDto { Title = "Agenda" }],
		}, organizerId);

		await svc.CancelAsync(created.Id, organizerId);

		// Meeting no longer visible via normal query (soft-deleted)
		var act = async () => await svc.GetAsync(created.Id, organizerId);
		await act.Should().ThrowAsync<KeyNotFoundException>();

		// Verify soft-deletes in DB
		var meeting = await db.Meetings.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == created.Id);
		meeting!.IsDeleted.Should().BeTrue();

		var attendees = await db.MeetingAttendees.IgnoreQueryFilters().Where(a => a.MeetingId == created.Id).ToListAsync();
		attendees.Should().AllSatisfy(a => a.IsDeleted.Should().BeTrue());

		var agenda = await db.MeetingAgendaItems.IgnoreQueryFilters().Where(a => a.MeetingId == created.Id).ToListAsync();
		agenda.Should().AllSatisfy(a => a.IsDeleted.Should().BeTrue());

		var evt = await db.CalendarEvents.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == created.CalendarEventId);
		evt!.IsDeleted.Should().BeTrue();
	}
}
