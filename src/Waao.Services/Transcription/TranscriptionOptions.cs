namespace Waao.Services.Transcription;

public record TranscriptionOptions
{
	/// <summary>Shared secret the LiveKit agent sends in X-Transcription-Key to authenticate ingest requests.</summary>
	public string IngestKey { get; init; } = string.Empty;
}
