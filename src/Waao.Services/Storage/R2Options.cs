namespace Waao.Services.Storage;

public class R2Options
{
	public string AccountId { get; set; } = string.Empty;
	public string AccessKey { get; set; } = string.Empty;
	public string SecretKey { get; set; } = string.Empty;
	public string Bucket { get; set; } = string.Empty;
	public string? PublicBaseUrl { get; set; }

	public bool IsConfigured =>
		!string.IsNullOrWhiteSpace(AccountId)
		&& !string.IsNullOrWhiteSpace(AccessKey)
		&& !string.IsNullOrWhiteSpace(SecretKey)
		&& !string.IsNullOrWhiteSpace(Bucket);

	public string Endpoint => $"https://{AccountId}.r2.cloudflarestorage.com";

	public string PublicUrlFor(string key) =>
		!string.IsNullOrWhiteSpace(PublicBaseUrl)
			? $"{PublicBaseUrl!.TrimEnd('/')}/{key}"
			: $"https://pub-{AccountId}.r2.dev/{Bucket}/{key}";
}
