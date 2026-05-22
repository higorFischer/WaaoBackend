namespace Waao.Services.Video;

public record JaasOptions
{
	public string AppId { get; init; } = string.Empty;
	public string KeyId { get; init; } = string.Empty;
	public string PrivateKey { get; init; } = string.Empty;
}
