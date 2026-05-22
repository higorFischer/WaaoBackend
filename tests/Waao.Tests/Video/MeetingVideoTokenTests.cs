using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Services.Services;
using Waao.Services.Video;
using Waao.Tests.Support;

namespace Waao.Tests.Video;

public class MeetingVideoTokenTests
{
	private static (MeetingService svc, Waao.Infra.EF.WaaoDbContext db) Build()
	{
		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		// Use NullLiveKitTokenService — we're testing access control, not JWT signing
		var svc = new MeetingService(db, calSvc, NullNotificationService.Instance, NullLiveKitTokenService.Instance, Options.Create(new LiveKitOptions { Url = "wss://test.invalid", ApiKey = "key", ApiSecret = "secret" }));
		return (svc, db);
	}

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name = "Test User")
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLowerInvariant()}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
			RoleKind = CollaboratorRoleKind.Collaborator,
		});
		await db.SaveChangesAsync();
		return id;
	}

	private static async Task<Guid> SeedMeetingWithAttendees(
		Waao.Infra.EF.WaaoDbContext db,
		Guid organizerId,
		Guid attendeeId)
	{
		var calendarId = Guid.CreateVersion7();
		db.Calendars.Add(new Waao.Domain.Models.Entities.Calendar.Calendar
		{
			Id = calendarId,
			OwnerId = organizerId,
			Name = "Personal",
			Scope = Waao.Domain.Models.Enums.CalendarScope.Personal,
			CreatedAt = DateTime.UtcNow,
		});

		var eventId = Guid.CreateVersion7();
		db.CalendarEvents.Add(new CalendarEvent
		{
			Id = eventId,
			CalendarId = calendarId,
			CreatedById = organizerId,
			Title = "Team Sync",
			StartsAtUtc = DateTime.UtcNow.AddHours(1),
			EndsAtUtc = DateTime.UtcNow.AddHours(2),
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

		await db.SaveChangesAsync();
		return meetingId;
	}

	[Fact]
	public async Task GetVideoToken_Organizer_ReturnsModeratorsTrue()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var meetingId = await SeedMeetingWithAttendees(db, organizerId, attendeeId);

		var result = await svc.GetVideoTokenAsync(meetingId, organizerId);

		result.Should().NotBeNull();
		result.Url.Should().Be("wss://test.invalid");
		result.Room.Should().Be($"waao-{meetingId:N}");
		// NullLiveKitTokenService always returns "test-jwt-token"
		result.Token.Should().Be("test-jwt-token");
	}

	[Fact]
	public async Task GetVideoToken_Attendee_ReturnsModeratorsToken()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var meetingId = await SeedMeetingWithAttendees(db, organizerId, attendeeId);

		// Attendee can get a token
		var result = await svc.GetVideoTokenAsync(meetingId, attendeeId);

		result.Should().NotBeNull();
		result.Token.Should().Be("test-jwt-token");
	}

	[Fact]
	public async Task GetVideoToken_NonMember_ThrowsUnauthorizedAccessException()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var nonMemberId = await SeedCollaborator(db, "Outsider");
		var meetingId = await SeedMeetingWithAttendees(db, organizerId, attendeeId);

		var act = async () => await svc.GetVideoTokenAsync(meetingId, nonMemberId);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task GetVideoToken_UnknownMeeting_ThrowsKeyNotFoundException()
	{
		var (svc, db) = Build();
		var callerId = await SeedCollaborator(db, "Caller");

		var act = async () => await svc.GetVideoTokenAsync(Guid.CreateVersion7(), callerId);

		await act.Should().ThrowAsync<KeyNotFoundException>();
	}

	[Fact]
	public async Task GetVideoToken_Organizer_ModeratorFlagIsTrue_ViaRealTokenService()
	{
		// Use a real LiveKitTokenService with a throwaway HMAC secret to verify moderator flag
		const string testSecret = "test-secret-must-be-at-least-32-chars!!";

		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var options = Options.Create(new LiveKitOptions
		{
			Url = "wss://test.invalid",
			ApiKey = "test-key",
			ApiSecret = testSecret,
		});
		var liveKitTokenSvc = new LiveKitTokenService(options);
		var svc = new MeetingService(db, calSvc, NullNotificationService.Instance, liveKitTokenSvc, options);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var meetingId = await SeedMeetingWithAttendees(db, organizerId, attendeeId);

		// Organizer gets moderator=true in JWT metadata
		var organizerResult = await svc.GetVideoTokenAsync(meetingId, organizerId);
		var organizerJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
			.ReadJwtToken(organizerResult.Token);
		var metadataJson = organizerJwt.Payload["metadata"]?.ToString()
			?? throw new InvalidOperationException("metadata claim missing");
		var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
		doc.RootElement.GetProperty("moderator").GetBoolean().Should().BeTrue("organizer must be moderator");

		// Attendee gets moderator=false in JWT metadata
		var attendeeResult = await svc.GetVideoTokenAsync(meetingId, attendeeId);
		var attendeeJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
			.ReadJwtToken(attendeeResult.Token);
		var attendeeMetadataJson = attendeeJwt.Payload["metadata"]?.ToString()
			?? throw new InvalidOperationException("metadata claim missing");
		var attendeeDoc = System.Text.Json.JsonDocument.Parse(attendeeMetadataJson);
		attendeeDoc.RootElement.GetProperty("moderator").GetBoolean().Should().BeFalse("attendee must not be moderator");
	}
}
