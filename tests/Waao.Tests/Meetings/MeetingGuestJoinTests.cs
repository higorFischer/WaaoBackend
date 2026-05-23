using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Services;
using Waao.Services.Video;
using Waao.Tests.Support;

namespace Waao.Tests.Meetings;

public class MeetingGuestJoinTests
{
	private static (MeetingService svc, Waao.Infra.EF.WaaoDbContext db) Build()
	{
		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var svc = new MeetingService(db, calSvc, NullNotificationService.Instance, NullLiveKitTokenService.Instance, Options.Create(new LiveKitOptions { Url = "wss://test.invalid" }));
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

	private static CreateMeetingDto BasicCreateDto(Guid attendeeId) => new()
	{
		Title = "Guest Test Meeting",
		StartsAtUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
		EndsAtUtc = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
		AttendeeCollaboratorIds = [attendeeId],
		AttendeeDepartmentIds = [],
		Agenda = [],
	};

	// ---- CreateAsync: GuestToken is generated and non-empty ----

	[Fact]
	public async Task CreateAsync_SetsNonEmptyGuestToken()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var result = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);

		var meeting = await db.Meetings.FindAsync(result.Id);
		meeting!.GuestToken.Should().NotBeNullOrWhiteSpace();
		meeting.GuestToken.Length.Should().BeGreaterThanOrEqualTo(32);
	}

	// ---- GetGuestLinkAsync: organizer gets the token ----

	[Fact]
	public async Task GetGuestLinkAsync_Organizer_ReturnsToken()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);
		var link = await svc.GetGuestLinkAsync(created.Id, organizerId);

		link.Token.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task GetGuestLinkAsync_NonOrganizer_ThrowsUnauthorized()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);

		var act = async () => await svc.GetGuestLinkAsync(created.Id, attendeeId);
		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task GetGuestLinkAsync_Admin_ReturnsToken()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var adminId = await SeedCollaborator(db, "Admin", CollaboratorRoleKind.Admin);

		var created = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);
		var link = await svc.GetGuestLinkAsync(created.Id, adminId);

		link.Token.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task GetGuestLinkAsync_UnknownMeeting_ThrowsKeyNotFound()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");

		var act = async () => await svc.GetGuestLinkAsync(Guid.CreateVersion7(), organizerId);
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}

	// ---- JoinAsGuestAsync: valid token -> returns LiveKit credentials ----

	[Fact]
	public async Task JoinAsGuestAsync_ValidToken_ReturnsLiveKitCredentials()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);
		var link = await svc.GetGuestLinkAsync(created.Id, organizerId);

		var result = await svc.JoinAsGuestAsync(created.Id, new GuestJoinRequestDto
		{
			Token = link.Token,
			DisplayName = "External Guest",
		});

		result.Should().NotBeNull();
		result.LiveKitToken.Should().NotBeNullOrWhiteSpace();
		result.LiveKitUrl.Should().Be("wss://test.invalid");
		result.MeetingTitle.Should().Be("Guest Test Meeting");
	}

	[Fact]
	public async Task JoinAsGuestAsync_InvalidToken_ThrowsUnauthorized()
	{
		var (svc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");

		var created = await svc.CreateAsync(BasicCreateDto(attendeeId), organizerId);

		var act = async () => await svc.JoinAsGuestAsync(created.Id, new GuestJoinRequestDto
		{
			Token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", // wrong token (33 chars)
			DisplayName = "Hacker",
		});
		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	[Fact]
	public async Task JoinAsGuestAsync_UnknownMeeting_ThrowsKeyNotFound()
	{
		var (svc, db) = Build();

		var act = async () => await svc.JoinAsGuestAsync(Guid.CreateVersion7(), new GuestJoinRequestDto
		{
			Token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
			DisplayName = "Guest",
		});
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}
}
