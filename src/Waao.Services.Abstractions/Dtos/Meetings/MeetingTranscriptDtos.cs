namespace Waao.Services.Abstractions.Dtos.Meetings;

public record MeetingTranscriptSummaryDto
{
	public Guid MeetingId { get; init; }
	public string MeetingTitle { get; init; } = string.Empty;
	public DateTime MeetingStartsAtUtc { get; init; }
	public DateTime GeneratedAtUtc { get; init; }
	public int LineCount { get; init; }
}

public record IngestTranscriptDto
{
	/// <summary>UTC instant when this recording session began (agent join). Stamped
	/// onto every ingested line so re-joining a meeting produces a separate block
	/// on the transcript instead of appending into the previous one.</summary>
	public DateTime? RecordingStartedAtUtc { get; init; }

	public IReadOnlyList<IngestTranscriptLineDto> Lines { get; init; } = [];
}

public record IngestTranscriptLineDto
{
	public Guid? SpeakerId { get; init; }
	public string SpeakerName { get; init; } = string.Empty;
	public string Text { get; init; } = string.Empty;
	public int OffsetSeconds { get; init; }
}

public record MeetingTranscriptDto
{
	public Guid MeetingId { get; init; }
	public DateTime GeneratedAtUtc { get; init; }
	public IReadOnlyList<MeetingTranscriptLineDto> Lines { get; init; } = [];
}

public record MeetingTranscriptLineDto
{
	public Guid? SpeakerCollaboratorId { get; init; }
	public string SpeakerName { get; init; } = string.Empty;
	public string Text { get; init; } = string.Empty;
	public int OffsetSeconds { get; init; }
	/// <summary>When the recording session for this line started. Used by the
	/// frontend to group lines into separate blocks (one per re-recording).</summary>
	public DateTime? RecordingStartedAtUtc { get; init; }
}
