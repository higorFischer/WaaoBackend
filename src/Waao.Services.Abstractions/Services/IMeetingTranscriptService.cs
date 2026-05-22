using Waao.Services.Abstractions.Dtos.Meetings;

namespace Waao.Services.Abstractions.Services;

public interface IMeetingTranscriptService
{
	/// <summary>
	/// Ingest (overwrite) a transcript for the given meeting.
	/// Verifies the meeting exists; deletes any existing transcript; creates a new one with lines.
	/// Speaker resolution: if <see cref="IngestTranscriptLineDto.SpeakerId"/> resolves to a live collaborator the id is kept, otherwise left null.
	/// </summary>
	Task IngestAsync(Guid meetingId, IngestTranscriptDto dto, CancellationToken ct = default);

	/// <summary>
	/// Returns the transcript for the given meeting, or null if none exists.
	/// The caller must be organizer or attendee; throws <see cref="UnauthorizedAccessException"/> otherwise.
	/// </summary>
	Task<MeetingTranscriptDto?> GetAsync(Guid meetingId, Guid callerId, CancellationToken ct = default);
}
