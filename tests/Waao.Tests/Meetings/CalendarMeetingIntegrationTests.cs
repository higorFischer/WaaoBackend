using FluentAssertions;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Calendar;
using Waao.Services.Services;
using Waao.Tests.Support;

namespace Waao.Tests.Meetings;

public class CalendarMeetingIntegrationTests
{
	private static (MeetingService meetingSvc, CalendarEventService evtSvc, CalendarService calSvc, Waao.Infra.EF.WaaoDbContext db)
		Build()
	{
		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var expander = new RecurrenceExpander();
		var evtSvc = new CalendarEventService(db, expander, calSvc);
		var meetingSvc = new MeetingService(db, calSvc, NullNotificationService.Instance);
		return (meetingSvc, evtSvc, calSvc, db);
	}

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name)
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLowerInvariant()}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
		});
		await db.SaveChangesAsync();
		return id;
	}

	// ---- Invited attendee sees meeting event in GetOccurrencesAsync; occurrence carries MeetingId ----

	[Fact]
	public async Task GetOccurrencesAsync_InvitedAttendee_SeesMeetingEventWithMeetingId()
	{
		var (meetingSvc, evtSvc, calSvc, db) = Build();

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		// Ensure attendee has a personal calendar (so they appear in ListVisibleCalendars)
		await calSvc.EnsurePersonalCalendarAsync(attendeeId);

		var start = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
		var created = await meetingSvc.CreateAsync(new CreateMeetingDto
		{
			Title = "Cross-Calendar Meeting",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			AttendeeCollaboratorIds = [attendeeId],
			AttendeeDepartmentIds = [],
			Agenda = [],
		}, organizerId);

		// Attendee queries their own occurrences in the window
		var query = new EventWindowQueryDto
		{
			FromUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
			ToUtc = new DateTime(2026, 6, 10, 23, 59, 59, DateTimeKind.Utc),
		};
		var occurrences = await evtSvc.GetOccurrencesAsync(query, attendeeId);

		// The meeting event should appear even though it's on organizer's calendar
		occurrences.Should().Contain(o => o.EventId == created.CalendarEventId);

		// The occurrence should carry the MeetingId
		var meetingOcc = occurrences.First(o => o.EventId == created.CalendarEventId);
		meetingOcc.MeetingId.Should().Be(created.Id);
	}

	[Fact]
	public async Task GetOccurrencesAsync_Organizer_SeesMeetingEventWithMeetingId()
	{
		var (meetingSvc, evtSvc, calSvc, db) = Build();

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var start = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
		var created = await meetingSvc.CreateAsync(new CreateMeetingDto
		{
			Title = "Organizer Meeting",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			AttendeeCollaboratorIds = [attendeeId],
			AttendeeDepartmentIds = [],
			Agenda = [],
		}, organizerId);

		var query = new EventWindowQueryDto
		{
			FromUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
			ToUtc = new DateTime(2026, 6, 10, 23, 59, 59, DateTimeKind.Utc),
		};

		var occurrences = await evtSvc.GetOccurrencesAsync(query, organizerId);

		// The organizer should see the meeting occurrence with MeetingId set
		var meetingOcc = occurrences.FirstOrDefault(o => o.EventId == created.CalendarEventId);
		meetingOcc.Should().NotBeNull();
		meetingOcc!.MeetingId.Should().Be(created.Id);
	}

	[Fact]
	public async Task GetOccurrencesAsync_PlainEvent_HasNullMeetingId()
	{
		var (meetingSvc, evtSvc, calSvc, db) = Build();
		var userId = await SeedCollaborator(db, "User");
		var calId = await calSvc.EnsurePersonalCalendarAsync(userId);

		var start = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
		await evtSvc.CreateAsync(new Waao.Services.Abstractions.Dtos.Calendar.CreateCalendarEventDto
		{
			CalendarId = calId,
			Title = "Plain Event",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
		}, userId);

		var query = new EventWindowQueryDto
		{
			FromUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
			ToUtc = new DateTime(2026, 6, 10, 23, 59, 59, DateTimeKind.Utc),
		};

		var occurrences = await evtSvc.GetOccurrencesAsync(query, userId);

		// Plain event should have null MeetingId
		occurrences.Should().Contain(o => o.Title == "Plain Event" && o.MeetingId == null);
	}
}
