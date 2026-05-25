namespace Waao.Services.Abstractions.Dtos.Messaging;

public record CardRefDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string BoardSlug { get; init; } = string.Empty;
	public string BoardTitle { get; init; } = string.Empty;
}

public record MeetingRefDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public DateTime StartsAtUtc { get; init; }
}
