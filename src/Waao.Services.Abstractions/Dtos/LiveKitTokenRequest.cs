namespace Waao.Services.Abstractions.Dtos;

public record LiveKitTokenRequest
{
	public Guid CollaboratorId { get; init; }
	public string Name { get; init; } = string.Empty;
	public string Room { get; init; } = string.Empty;
	public bool Moderator { get; init; }
}
