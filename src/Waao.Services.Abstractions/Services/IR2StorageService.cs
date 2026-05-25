namespace Waao.Services.Abstractions.Services;

public interface IR2StorageService
{
	bool IsEnabled { get; }

	/// <summary>Uploads a stream to R2 under the given key. Returns the public URL.</summary>
	Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default);
}
