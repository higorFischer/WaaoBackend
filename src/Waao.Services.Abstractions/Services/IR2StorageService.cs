namespace Waao.Services.Abstractions.Services;

public interface IR2StorageService
{
	bool IsEnabled { get; }

	/// <summary>True when a dedicated private bucket is configured for chat attachments.</summary>
	bool HasPrivateBucket { get; }

	/// <summary>Uploads a stream to the PUBLIC R2 bucket under the given key. Returns the public URL.
	/// Used for content that is intentionally shareable/cacheable (e.g. avatars).</summary>
	Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);

	/// <summary>Uploads a stream to the PRIVATE R2 bucket under the given key. Returns the stored object
	/// key (not a URL) — callers presign a short-lived GET URL via <see cref="GetPresignedUrl"/>.</summary>
	Task<string> UploadPrivateAsync(string key, Stream content, string contentType, CancellationToken ct = default);

	/// <summary>Builds a short-lived presigned GET URL for a private object key.</summary>
	string GetPresignedUrl(string key, TimeSpan ttl);
}
