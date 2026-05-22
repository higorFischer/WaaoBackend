namespace Waao.Services.Abstractions.Dtos.Meetings;

public record MeetingVideoTokenDto
{
	public string Token { get; init; } = string.Empty;

	/// <summary>WebSocket URL of the LiveKit SFU the client connects to.</summary>
	public string Url { get; init; } = string.Empty;

	public string Room { get; init; } = string.Empty;
}
