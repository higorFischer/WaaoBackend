namespace Waao.Domain.Models.Entities.Meetings;

public class MeetingTranscriptLine : Entity
{
	public Guid TranscriptId { get; set; }
	public virtual MeetingTranscript Transcript { get; set; } = null!;

	public Guid? SpeakerCollaboratorId { get; set; }
	public virtual Collaborator? SpeakerCollaborator { get; set; }

	public string SpeakerName { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;

	/// <summary>Seconds since this recording session started.</summary>
	public int OffsetSeconds { get; set; }

	/// <summary>
	/// When the recording session this line belongs to started. A single meeting
	/// can be joined and transcribed multiple times — each join produces a fresh
	/// session, and the frontend renders them as separate blocks ("Gerado em ...").
	/// Nullable for backwards compatibility with lines ingested before this field
	/// existed; new agent payloads always set it.
	/// </summary>
	public DateTime? RecordingStartedAtUtc { get; set; }
}
