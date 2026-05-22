namespace Waao.Services.Abstractions.Dtos.Meetings;

public record MeetingVideoTokenDto
{
	public string Token { get; init; } = string.Empty;
	public string AppId { get; init; } = string.Empty;
	public string Room { get; init; } = string.Empty;
}
