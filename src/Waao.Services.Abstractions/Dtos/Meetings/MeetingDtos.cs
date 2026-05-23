using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Meetings;

public record MeetingDto
{
	public Guid Id { get; init; }
	public Guid CalendarEventId { get; init; }
	public Guid OrganizerId { get; init; }
	public string OrganizerName { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? RecurrenceRule { get; init; }
	public bool IsRecurring { get; init; }
	public bool TranscriptionEnabled { get; init; }
	public IReadOnlyList<MeetingAttendeeDto> Attendees { get; init; } = [];
	public IReadOnlyList<MeetingAgendaItemDto> Agenda { get; init; } = [];
	public RsvpTallyDto RsvpTally { get; init; } = new();
	public MeetingRsvp MyRsvp { get; init; } = MeetingRsvp.NoResponse;
}

public record MeetingAttendeeDto
{
	public Guid Id { get; init; }
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public string? CollaboratorPhotoUrl { get; init; }
	public MeetingRsvp Rsvp { get; init; }
	public DateTime? RespondedAt { get; init; }
	public Guid? InvitedViaDepartmentId { get; init; }
	public string? InvitedViaDepartmentName { get; init; }
}

public record MeetingAgendaItemDto
{
	public Guid Id { get; init; }
	public int Order { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Notes { get; init; }
	public int? DurationMinutes { get; init; }
}

public record RsvpTallyDto
{
	public int Going { get; init; }
	public int Maybe { get; init; }
	public int Declined { get; init; }
	public int NoResponse { get; init; }
}

public record CreateMeetingDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? RecurrenceRule { get; init; }
	public DateTime? RecurrenceEndUtc { get; init; }
	public bool TranscriptionEnabled { get; init; } = false;
	public IReadOnlyList<Guid> AttendeeCollaboratorIds { get; init; } = [];
	public IReadOnlyList<Guid> AttendeeDepartmentIds { get; init; } = [];
	public IReadOnlyList<CreateAgendaItemDto> Agenda { get; init; } = [];
}

public record CreateAgendaItemDto
{
	public string Title { get; init; } = string.Empty;
	public string? Notes { get; init; }
	public int? DurationMinutes { get; init; }
}

public record UpdateMeetingDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? RecurrenceRule { get; init; }
	public DateTime? RecurrenceEndUtc { get; init; }
	public bool TranscriptionEnabled { get; init; } = false;
	public IReadOnlyList<Guid> AttendeeCollaboratorIds { get; init; } = [];
	public IReadOnlyList<Guid> AttendeeDepartmentIds { get; init; } = [];
	public IReadOnlyList<CreateAgendaItemDto> Agenda { get; init; } = [];
}

public record SetRsvpDto
{
	public MeetingRsvp Rsvp { get; init; }
}

public record TranscriptionEnabledDto
{
	public bool Enabled { get; init; }
}

/// <summary>Opaque guest link token returned to organizers. Build share URL as: /join/{meetingId}?token={Token}</summary>
public record GuestLinkDto
{
	public string Token { get; init; } = string.Empty;
}

/// <summary>Payload posted by an external guest to receive a LiveKit token.</summary>
public record GuestJoinRequestDto
{
	public string Token { get; init; } = string.Empty;
	public string DisplayName { get; init; } = string.Empty;
}

/// <summary>LiveKit credentials returned to a guest after a successful join request.</summary>
public record GuestJoinResultDto
{
	public string LiveKitUrl { get; init; } = string.Empty;
	public string LiveKitToken { get; init; } = string.Empty;
	public string MeetingTitle { get; init; } = string.Empty;
	public bool TranscriptionEnabled { get; init; }
}
