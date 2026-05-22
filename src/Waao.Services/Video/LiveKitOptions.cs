namespace Waao.Services.Video;

public record LiveKitOptions
{
	/// <summary>WebSocket URL of the LiveKit SFU, e.g. wss://waao-livekit.fly.dev.</summary>
	public string Url { get; init; } = string.Empty;

	public string ApiKey { get; init; } = string.Empty;

	/// <summary>HMAC signing secret. Must be at least 32 characters (HS256 requirement).</summary>
	public string ApiSecret { get; init; } = string.Empty;
}
