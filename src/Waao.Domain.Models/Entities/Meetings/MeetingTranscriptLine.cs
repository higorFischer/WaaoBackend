namespace Waao.Domain.Models.Entities.Meetings;

public class MeetingTranscriptLine : Entity
{
	public Guid TranscriptId { get; set; }
	public virtual MeetingTranscript Transcript { get; set; } = null!;

	public Guid? SpeakerCollaboratorId { get; set; }
	public virtual Collaborator? SpeakerCollaborator { get; set; }

	public string SpeakerName { get; set; } = string.Empty;
	public string Text { get; set; } = string.Empty;
	public int OffsetSeconds { get; set; }
}
