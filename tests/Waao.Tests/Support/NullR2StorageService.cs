using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Support;

/// <summary>No-op IR2StorageService for tests — storage disabled, presign echoes a fake URL.</summary>
public sealed class NullR2StorageService : IR2StorageService
{
	public static readonly NullR2StorageService Instance = new();

	public bool IsEnabled => false;
	public bool HasPrivateBucket => false;

	public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
		=> Task.FromResult($"https://test.invalid/{key}");

	public Task<string> UploadPrivateAsync(string key, Stream content, string contentType, CancellationToken ct = default)
		=> Task.FromResult(key);

	public string GetPresignedUrl(string key, TimeSpan ttl) => $"https://test.invalid/private/{key}";
}
