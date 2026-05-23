namespace Waao.Services.Abstractions.Dtos;

public record LiveKitTokenRequest
{
	public Guid CollaboratorId { get; init; }
	public string Name { get; init; } = string.Empty;
	public string Room { get; init; } = string.Empty;
	public bool Moderator { get; init; }
}

public record GuestLiveKitTokenRequest
{
	/// <summary>Opaque identity string for the guest participant (e.g. "guest-&lt;uuid&gt;").</summary>
	public string Identity { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string Room { get; init; } = string.Empty;
}
