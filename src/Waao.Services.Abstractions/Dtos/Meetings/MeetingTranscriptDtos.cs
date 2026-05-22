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
}
