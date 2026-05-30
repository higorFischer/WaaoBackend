namespace Waao.Services.Storage;

public class R2Options
{
	public string AccountId { get; set; } = string.Empty;
	public string AccessKey { get; set; } = string.Empty;
	public string SecretKey { get; set; } = string.Empty;
	public string Bucket { get; set; } = string.Empty;
	public string? PublicBaseUrl { get; set; }

	/// <summary>Private bucket for chat attachments (no public access) — served via short-lived presigned
	/// URLs. Falls back to <see cref="Bucket"/> when unset (no privacy gain, dev convenience).</summary>
	public string? PrivateBucket { get; set; }

	/// <summary>The bucket chat attachments are stored in/served from privately.</summary>
	public string PrivateBucketName => string.IsNullOrWhiteSpace(PrivateBucket) ? Bucket : PrivateBucket!;

	/// <summary>True when a dedicated private bucket is configured (distinct from the public one).</summary>
	public bool HasPrivateBucket => !string.IsNullOrWhiteSpace(PrivateBucket);

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
