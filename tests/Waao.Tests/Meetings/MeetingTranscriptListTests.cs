using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Services.Transcription;
using Waao.Tests.Support;
using Xunit;
using CalendarEntity = Waao.Domain.Models.Entities.Calendar.Calendar;

namespace Waao.Tests.Meetings;

public class MeetingTranscriptListTests
{
	private static MeetingTranscriptService Build() =>
		new(TestDb.New());

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name = "User")
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLowerInvariant()}{id:N}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
			RoleKind = CollaboratorRoleKind.Collaborator,
		});
		await db.SaveChangesAsync();
		return id;
	}

	/// <summary>
	/// Seeds a meeting with an organizer + one attendee, plus a transcript with the given number of lines.
	/// Returns (meetingId, transcriptId).
	/// </summary>
	private static async Task<(Guid meetingId, Guid transcriptId)> SeedMeetingWithTranscript(
		Waao.Infra.EF.WaaoDbContext db,
		Guid organizerId,
		Guid attendeeId,
		string title,
		DateTime startsAt,
		DateTime generatedAt,
		int lineCount)
	{
		var calendarId = Guid.CreateVersion7();
		db.Calendars.Add(new CalendarEntity
		{
			Id = calendarId,
			OwnerId = organizerId,
			Name = "Personal",
			Scope = CalendarScope.Personal,
			CreatedAt = DateTime.UtcNow,
		});

		var eventId = Guid.CreateVersion7();
		db.CalendarEvents.Add(new CalendarEvent
		{
			Id = eventId,
			CalendarId = calendarId,
			CreatedById = organizerId,
			Title = title,
			StartsAtUtc = startsAt,
			EndsAtUtc = startsAt.AddHours(1),
			CreatedAt = DateTime.UtcNow,
		});

		var meetingId = Guid.CreateVersion7();
		db.Meetings.Add(new Meeting
		{
			Id = meetingId,
			CalendarEventId = eventId,
			OrganizerId = organizerId,
			CreatedAt = DateTime.UtcNow,
		});

		db.MeetingAttendees.Add(new MeetingAttendee
		{
			Id = Guid.CreateVersion7(),
			MeetingId = meetingId,
			CollaboratorId = organizerId,
			Rsvp = MeetingRsvp.Going,
			CreatedAt = DateTime.UtcNow,
		});

		db.MeetingAttendees.Add(new MeetingAttendee
		{
			Id = Guid.CreateVersion7(),
			MeetingId = meetingId,
			CollaboratorId = attendeeId,
			Rsvp = MeetingRsvp.NoResponse,
			CreatedAt = DateTime.UtcNow,
		});

		var transcriptId = Guid.CreateVersion7();
		var transcript = new MeetingTranscript
		{
			Id = transcriptId,
			MeetingId = meetingId,
			GeneratedAtUtc = generatedAt,
			CreatedAt = DateTime.UtcNow,
		};
		db.MeetingTranscripts.Add(transcript);

		for (var i = 0; i < lineCount; i++)
		{
			db.MeetingTranscriptLines.Add(new MeetingTranscriptLine
			{
				Id = Guid.CreateVersion7(),
				TranscriptId = transcriptId,
				SpeakerName = "Speaker",
				Text = $"Line {i + 1}",
				OffsetSeconds = i * 10,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await db.SaveChangesAsync();
		return (meetingId, transcriptId);
	}

	[Fact]
	public async Task ListMineAsync_OrganizerSeesOwnTranscript()
	{
		var db = TestDb.New();
		var svc = new MeetingTranscriptService(db);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var outsiderId = await SeedCollaborator(db, "Outsider");

		await SeedMeetingWithTranscript(db, organizerId, attendeeId, "Team Sync",
			DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-30), lineCount: 5);

		var result = await svc.ListMineAsync(organizerId);

		result.Should().HaveCount(1);
		result[0].MeetingTitle.Should().Be("Team Sync");
		result[0].LineCount.Should().Be(5);
	}

	[Fact]
	public async Task ListMineAsync_AttendeeSeesTranscript()
	{
		var db = TestDb.New();
		var svc = new MeetingTranscriptService(db);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		await SeedMeetingWithTranscript(db, organizerId, attendeeId, "Retrospective",
			DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddMinutes(-60), lineCount: 3);

		var result = await svc.ListMineAsync(attendeeId);

		result.Should().HaveCount(1);
		result[0].MeetingTitle.Should().Be("Retrospective");
		result[0].LineCount.Should().Be(3);
	}

	[Fact]
	public async Task ListMineAsync_OutsiderSeesNothing()
	{
		var db = TestDb.New();
		var svc = new MeetingTranscriptService(db);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var outsiderId = await SeedCollaborator(db, "Outsider");

		await SeedMeetingWithTranscript(db, organizerId, attendeeId, "Private Meeting",
			DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-10), lineCount: 4);

		var result = await svc.ListMineAsync(outsiderId);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task ListMineAsync_OrderedNewestGeneratedAtFirst()
	{
		var db = TestDb.New();
		var svc = new MeetingTranscriptService(db);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var earlier = DateTime.UtcNow.AddHours(-5);
		var later = DateTime.UtcNow.AddHours(-1);

		await SeedMeetingWithTranscript(db, organizerId, attendeeId, "Old Meeting",
			DateTime.UtcNow.AddHours(-6), earlier, lineCount: 2);
		await SeedMeetingWithTranscript(db, organizerId, attendeeId, "New Meeting",
			DateTime.UtcNow.AddHours(-2), later, lineCount: 7);

		var result = await svc.ListMineAsync(organizerId);

		result.Should().HaveCount(2);
		result[0].MeetingTitle.Should().Be("New Meeting", "newest generatedAt comes first");
		result[0].LineCount.Should().Be(7);
		result[1].MeetingTitle.Should().Be("Old Meeting");
		result[1].LineCount.Should().Be(2);
	}

	[Fact]
	public async Task ListMineAsync_LineCountExcludesSoftDeletedLines()
	{
		var db = TestDb.New();
		var svc = new MeetingTranscriptService(db);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var (_, transcriptId) = await SeedMeetingWithTranscript(db, organizerId, attendeeId, "Meeting with deleted lines",
			DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(-5), lineCount: 4);

		// Soft-delete 2 of the 4 lines
		var lines = db.MeetingTranscriptLines.Where(l => l.TranscriptId == transcriptId).Take(2).ToList();
		foreach (var line in lines)
		{
			line.IsDeleted = true;
			line.DeletedAt = DateTime.UtcNow;
		}
		await db.SaveChangesAsync();

		var result = await svc.ListMineAsync(organizerId);

		result.Should().HaveCount(1);
		result[0].LineCount.Should().Be(2, "only 2 non-deleted lines remain");
	}
}
