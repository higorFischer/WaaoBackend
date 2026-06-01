namespace Waao.Services.Abstractions.Dtos.Calls;

public record CallParticipantDto
{
	public Guid CollaboratorId { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public DateTime JoinedAtUtc { get; init; }
}

public record CallChannelDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public int Position { get; init; }
	public Guid CreatedById { get; init; }
	public string CreatedByName { get; init; } = string.Empty;
	public IReadOnlyList<CallParticipantDto> Participants { get; init; } = [];
}

public record CreateCallChannelDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? ColorHex { get; init; }
}

public record UpdateCallChannelDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public int Position { get; init; }
}

public record CallTokenDto
{
	public string Token { get; init; } = string.Empty;
	public string Url { get; init; } = string.Empty;
	public string Room { get; init; } = string.Empty;
}
