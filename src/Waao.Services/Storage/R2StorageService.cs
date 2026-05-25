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

		using var s3 = BuildClient();
		var req = new PutObjectRequest
		{
			BucketName            = Options.Bucket,
			Key                   = key,
			InputStream           = buffer,
			ContentType           = contentType,
			DisablePayloadSigning = true,
		};

		try
		{
			await s3.PutObjectAsync(req, ct);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "R2 PutObject failed bucket={Bucket} key={Key} size={Size} contentType={ContentType}",
				Options.Bucket, key, buffer.Length, contentType);
			throw;
		}

		Logger.LogInformation("Uploaded to R2 key={Key} size={Size} contentType={ContentType}", key, buffer.Length, contentType);
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
