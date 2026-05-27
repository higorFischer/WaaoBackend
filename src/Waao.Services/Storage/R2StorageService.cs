using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Storage;

public sealed class R2StorageService(
	IOptions<R2Options> OptionsAccessor,
	ILogger<R2StorageService> Logger) : IR2StorageService
{
	private readonly R2Options Options = OptionsAccessor.Value;

	public bool IsEnabled => Options.IsConfigured;

	public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
	{
		if (!IsEnabled)
			throw new InvalidOperationException("R2 storage is not configured.");

		// Buffer to a seekable MemoryStream so the AWS SDK can compute Content-Length
		// without chunked-transfer/payload-signing — R2 rejects unsigned streaming PUTs
		// that come from non-seekable streams.
		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer, ct);
		buffer.Position = 0;
		// Capture length up front — the AWS SDK may dispose the stream after PutObject.
		var sizeBytes = buffer.Length;

		// Strip any RFC 7231 media-type parameters (e.g. ";codecs=opus" on a
		// MediaRecorder webm). The AWS SDK signs Content-Type into the SigV4
		// canonical request, and Cloudflare R2 normalizes the header before
		// re-computing the signature — semicolons in the value break that
		// round-trip and the upload comes back as SignatureDoesNotMatch (500).
		var safeContentType = contentType;
		var semi = safeContentType?.IndexOf(';');
		if (semi.HasValue && semi.Value > 0) safeContentType = safeContentType![..semi.Value].Trim();

		using var s3 = BuildClient();
		var req = new PutObjectRequest
		{
			BucketName            = Options.Bucket,
			Key                   = key,
			InputStream           = buffer,
			ContentType           = safeContentType,
			DisablePayloadSigning = true,
		};

		try
		{
			await s3.PutObjectAsync(req, ct);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "R2 PutObject failed bucket={Bucket} key={Key} size={Size} contentType={ContentType}",
				Options.Bucket, key, sizeBytes, contentType);
			throw;
		}

		Logger.LogInformation("Uploaded to R2 key={Key} size={Size} contentType={ContentType}", key, sizeBytes, contentType);
		return Options.PublicUrlFor(key);
	}

	private AmazonS3Client BuildClient()
	{
		var config = new AmazonS3Config
		{
			ServiceURL           = Options.Endpoint,
			ForcePathStyle       = true,
			AuthenticationRegion = "auto",
		};
		return new AmazonS3Client(Options.AccessKey, Options.SecretKey, config);
	}
}
