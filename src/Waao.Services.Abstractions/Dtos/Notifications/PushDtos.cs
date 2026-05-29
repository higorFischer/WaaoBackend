namespace Waao.Services.Abstractions.Dtos.Notifications;

public record SavePushSubscriptionDto
{
	public string Endpoint { get; init; } = string.Empty;
	public string P256dh { get; init; } = string.Empty;
	public string Auth { get; init; } = string.Empty;
}

public record UnsubscribeDto
{
	public string Endpoint { get; init; } = string.Empty;
}

public record PushTestDto
{
	public string? Title { get; init; }
	public string? Body { get; init; }
}
