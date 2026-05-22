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
		// Use NullJaasTokenService — we're testing access control, not JWT signing
		var svc = new MeetingService(db, calSvc, NullNotificationService.Instance, NullJaasTokenService.Instance, Options.Create(new JaasOptions { AppId = "test-app" }));
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
		result.AppId.Should().Be("test-app");
		result.Room.Should().Be($"waao-{meetingId:N}");
		// NullJaasTokenService always returns "test-jwt-token"
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
		// Use a real JaasTokenService with a throwaway RSA key to verify moderator flag
		using var rsa = System.Security.Cryptography.RSA.Create(2048);
		var privatePem = rsa.ExportRSAPrivateKeyPem();

		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var options = Options.Create(new JaasOptions
		{
			AppId = "test-app",
			KeyId = "test-app/key1",
			PrivateKey = privatePem,
		});
		var jaasTokenSvc = new JaasTokenService(options);
		var svc = new MeetingService(db, calSvc, NullNotificationService.Instance, jaasTokenSvc, options);

		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var meetingId = await SeedMeetingWithAttendees(db, organizerId, attendeeId);

		// Organizer gets moderator=true in JWT
		var organizerResult = await svc.GetVideoTokenAsync(meetingId, organizerId);
		var organizerJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
			.ReadJwtToken(organizerResult.Token);
		var contextJson = System.Text.Json.JsonSerializer.Serialize(organizerJwt.Payload["context"]);
		var doc = System.Text.Json.JsonDocument.Parse(contextJson);
		doc.RootElement.GetProperty("user").GetProperty("moderator").GetBoolean().Should().BeTrue("organizer must be moderator");

		// Attendee gets moderator=false in JWT
		var attendeeResult = await svc.GetVideoTokenAsync(meetingId, attendeeId);
		var attendeeJwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
			.ReadJwtToken(attendeeResult.Token);
		var attendeeContextJson = System.Text.Json.JsonSerializer.Serialize(attendeeJwt.Payload["context"]);
		var attendeeDoc = System.Text.Json.JsonDocument.Parse(attendeeContextJson);
		attendeeDoc.RootElement.GetProperty("user").GetProperty("moderator").GetBoolean().Should().BeFalse("attendee must not be moderator");
	}
}
