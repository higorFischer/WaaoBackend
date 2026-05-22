using Waao.Services.Abstractions.Dtos.Meetings;

namespace Waao.Services.Abstractions.Services;

public interface IMeetingService
{
	/// <summary>
	/// Creates a meeting with a backing CalendarEvent on the organizer's personal calendar.
	/// Attendees are expanded from direct collaborator ids and department ids; organizer is auto-added as Going.
	/// </summary>
	Task<MeetingDto> CreateAsync(CreateMeetingDto dto, Guid organizerId, CancellationToken ct = default);

	/// <summary>
	/// Returns the meeting if the caller is organizer, an attendee, or Admin; else throws KeyNotFoundException.
	/// MyRsvp reflects the caller's current RSVP.
	/// </summary>
	Task<MeetingDto> GetAsync(Guid meetingId, Guid callerId, CancellationToken ct = default);

	/// <summary>Organizer or Admin only. Re-expands attendees (diffs); replaces agenda.</summary>
	Task<MeetingDto> UpdateAsync(Guid meetingId, UpdateMeetingDto dto, Guid callerId, CancellationToken ct = default);

	/// <summary>Organizer or Admin only. Soft-deletes the meeting, attendees, agenda items, and backing event.</summary>
	Task CancelAsync(Guid meetingId, Guid callerId, CancellationToken ct = default);

	/// <summary>Caller must be an attendee. Sets their RSVP and stamps RespondedAt.</summary>
	Task<MeetingDto> SetRsvpAsync(Guid meetingId, SetRsvpDto dto, Guid callerId, CancellationToken ct = default);

	/// <summary>Returns meetings the caller organizes or attends, within the given window.</summary>
	Task<IReadOnlyList<MeetingDto>> ListMyMeetingsAsync(Guid callerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

	/// <summary>
	/// Mints a LiveKit video token for the given meeting.
	/// Caller must be the organizer or an attendee; else throws KeyNotFoundException (unknown meeting) or UnauthorizedAccessException (not a member).
	/// Moderator flag is true only for the organizer.
	/// </summary>
	Task<MeetingVideoTokenDto> GetVideoTokenAsync(Guid meetingId, Guid callerId, CancellationToken ct = default);

	/// <summary>
	/// Returns whether transcription is enabled for the given meeting.
	/// Throws KeyNotFoundException if the meeting does not exist.
	/// Intended for the LiveKit agent worker (authenticated via X-Transcription-Key).
	/// </summary>
	Task<TranscriptionEnabledDto> GetTranscriptionEnabledAsync(Guid meetingId, CancellationToken ct = default);
}
