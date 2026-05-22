namespace Waao.Services.Abstractions.Dtos;

public record JaasTokenRequest
{
	public Guid CollaboratorId { get; init; }
	public string Name { get; init; } = string.Empty;
	public string Email { get; init; } = string.Empty;
	public string? Avatar { get; init; }
	public string Room { get; init; } = string.Empty;
	public bool Moderator { get; init; }
}
