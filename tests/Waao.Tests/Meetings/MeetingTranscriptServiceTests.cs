using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Entities.Meetings;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Services;
using Waao.Services.Transcription;
using Waao.Services.Video;
using Waao.Tests.Support;

namespace Waao.Tests.Meetings;

public class MeetingTranscriptServiceTests
{
	private static (MeetingTranscriptService transcriptSvc, MeetingService meetingSvc, Waao.Infra.EF.WaaoDbContext db)
		Build()
	{
		var db = TestDb.New();
		var calSvc = new CalendarService(db);
		var meetingSvc = new MeetingService(db, calSvc, NullNotificationService.Instance, NullLiveKitTokenService.Instance, Options.Create(new LiveKitOptions()));
		var transcriptSvc = new MeetingTranscriptService(db);
		return (transcriptSvc, meetingSvc, db);
	}

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name = "Test User")
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id,
			FullName = name,
			Email = $"{name.Replace(" ", "").ToLowerInvariant()}_{id:N}@test.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Status = CollaboratorStatus.Active,
		});
		await db.SaveChangesAsync();
		return id;
	}

	private static async Task<Guid> SeedMeeting(MeetingService meetingSvc, Guid organizerId, IReadOnlyList<Guid>? attendeeIds = null)
	{
		var start = DateTime.UtcNow.AddHours(1);
		var dto = await meetingSvc.CreateAsync(new CreateMeetingDto
		{
			Title = "Transcript Test Meeting",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			AttendeeCollaboratorIds = attendeeIds ?? [],
			AttendeeDepartmentIds = [],
			Agenda = [],
		}, organizerId);
		return dto.Id;
	}

	// ---- IngestAsync creates transcript and lines ----

	[Fact]
	public async Task IngestAsync_CreatesTranscriptWithLines()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var dto = new IngestTranscriptDto
		{
			Lines =
			[
				new IngestTranscriptLineDto { SpeakerId = null, SpeakerName = "Alice", Text = "Hello", OffsetSeconds = 0 },
				new IngestTranscriptLineDto { SpeakerId = null, SpeakerName = "Bob", Text = "Hi there", OffsetSeconds = 5 },
			],
		};

		await transcriptSvc.IngestAsync(meetingId, dto);

		var result = await transcriptSvc.GetAsync(meetingId, organizerId);

		result.Should().NotBeNull();
		result!.MeetingId.Should().Be(meetingId);
		result.Lines.Should().HaveCount(2);
		result.Lines[0].SpeakerName.Should().Be("Alice");
		result.Lines[1].SpeakerName.Should().Be("Bob");
	}

	// ---- IngestAsync second call overwrites the first ----

	[Fact]
	public async Task IngestAsync_SecondIngest_OverwritesPrevious()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var first = new IngestTranscriptDto
		{
			Lines = [new IngestTranscriptLineDto { SpeakerName = "Alice", Text = "First transcript", OffsetSeconds = 0 }],
		};
		await transcriptSvc.IngestAsync(meetingId, first);

		var second = new IngestTranscriptDto
		{
			Lines =
			[
				new IngestTranscriptLineDto { SpeakerName = "Bob", Text = "Second transcript line 1", OffsetSeconds = 0 },
				new IngestTranscriptLineDto { SpeakerName = "Bob", Text = "Second transcript line 2", OffsetSeconds = 3 },
			],
		};
		await transcriptSvc.IngestAsync(meetingId, second);

		var result = await transcriptSvc.GetAsync(meetingId, organizerId);

		result.Should().NotBeNull();
		result!.Lines.Should().HaveCount(2);
		result.Lines.Should().NotContain(l => l.Text == "First transcript");
		result.Lines[0].SpeakerName.Should().Be("Bob");
	}

	// ---- IngestAsync: unknown SpeakerId results in null SpeakerCollaboratorId ----

	[Fact]
	public async Task IngestAsync_UnknownSpeakerId_KeepsLineWithNullSpeakerCollaboratorId()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var unknownId = Guid.CreateVersion7();
		var dto = new IngestTranscriptDto
		{
			Lines =
			[
				new IngestTranscriptLineDto { SpeakerId = unknownId, SpeakerName = "Ghost", Text = "Phantom text", OffsetSeconds = 0 },
			],
		};

		await transcriptSvc.IngestAsync(meetingId, dto);

		var result = await transcriptSvc.GetAsync(meetingId, organizerId);

		result.Should().NotBeNull();
		result!.Lines.Should().HaveCount(1);
		result.Lines[0].SpeakerCollaboratorId.Should().BeNull();
		result.Lines[0].SpeakerName.Should().Be("Ghost");
	}

	// ---- IngestAsync: known SpeakerId is resolved ----

	[Fact]
	public async Task IngestAsync_KnownSpeakerId_IsResolvedToCollaborator()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var speakerId = await SeedCollaborator(db, "Known Speaker");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var dto = new IngestTranscriptDto
		{
			Lines =
			[
				new IngestTranscriptLineDto { SpeakerId = speakerId, SpeakerName = "Known Speaker", Text = "Known text", OffsetSeconds = 0 },
			],
		};

		await transcriptSvc.IngestAsync(meetingId, dto);

		var result = await transcriptSvc.GetAsync(meetingId, organizerId);

		result!.Lines[0].SpeakerCollaboratorId.Should().Be(speakerId);
	}

	// ---- GetAsync: caller not organizer or attendee throws UnauthorizedAccessException ----

	[Fact]
	public async Task GetAsync_CallerNotOrganizerOrAttendee_ThrowsUnauthorizedAccessException()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var outsiderId = await SeedCollaborator(db, "Outsider");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var dto = new IngestTranscriptDto
		{
			Lines = [new IngestTranscriptLineDto { SpeakerName = "Alice", Text = "Hello", OffsetSeconds = 0 }],
		};
		await transcriptSvc.IngestAsync(meetingId, dto);

		var act = async () => await transcriptSvc.GetAsync(meetingId, outsiderId);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	// ---- GetAsync: attendee can read ----

	[Fact]
	public async Task GetAsync_AttendeeCanRead()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var attendeeId = await SeedCollaborator(db, "Attendee");
		var meetingId = await SeedMeeting(meetingSvc, organizerId, [attendeeId]);

		var dto = new IngestTranscriptDto
		{
			Lines = [new IngestTranscriptLineDto { SpeakerName = "Alice", Text = "Hello", OffsetSeconds = 0 }],
		};
		await transcriptSvc.IngestAsync(meetingId, dto);

		var result = await transcriptSvc.GetAsync(meetingId, attendeeId);

		result.Should().NotBeNull();
	}

	// ---- GetAsync: returns null when no transcript exists ----

	[Fact]
	public async Task GetAsync_NoTranscript_ReturnsNull()
	{
		var (transcriptSvc, meetingSvc, db) = Build();
		var organizerId = await SeedCollaborator(db, "Organizer");
		var meetingId = await SeedMeeting(meetingSvc, organizerId);

		var result = await transcriptSvc.GetAsync(meetingId, organizerId);

		result.Should().BeNull();
	}

	// ---- IngestAsync: unknown meeting throws KeyNotFoundException ----

	[Fact]
	public async Task IngestAsync_UnknownMeeting_ThrowsKeyNotFoundException()
	{
		var (transcriptSvc, _, _) = Build();

		var act = async () => await transcriptSvc.IngestAsync(Guid.CreateVersion7(), new IngestTranscriptDto { Lines = [] });

		await act.Should().ThrowAsync<KeyNotFoundException>();
	}
}
