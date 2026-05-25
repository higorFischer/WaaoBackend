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

		using var s3 = BuildClient();
		var req = new PutObjectRequest
		{
			BucketName  = Options.Bucket,
			Key         = key,
			InputStream = content,
			ContentType = contentType,
			DisablePayloadSigning = true,
			DisableDefaultChecksumValidation = true,
		};

		await s3.PutObjectAsync(req, ct);
		Logger.LogInformation("Uploaded to R2 key={Key} contentType={ContentType}", key, contentType);
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
