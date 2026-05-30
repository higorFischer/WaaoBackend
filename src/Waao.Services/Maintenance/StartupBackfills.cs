using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Services;
using Waao.Services.Storage;

namespace Waao.Services.Maintenance;

/// <summary>
/// One-time, idempotent data backfills run at startup (inside the migrate/seed advisory lock).
/// Both only touch not-yet-migrated rows, so they no-op once complete and are safe to re-run.
/// </summary>
public static class StartupBackfills
{
	private const string EncPrefix = "enc:1:";

	/// <summary>Encrypt any message body still stored as plaintext (no <c>enc:1:</c> prefix). Includes
	/// soft-deleted rows. No-ops when encryption is disabled or once every row is encrypted.</summary>
	public static async Task EncryptLegacyMessageBodiesAsync(
		WaaoDbContext db, IMessageTextProtector protector, ILogger logger, CancellationToken ct = default)
	{
		if (!protector.IsEnabled) return;

		const int batchSize = 500;
		var total = 0;

		while (!ct.IsCancellationRequested)
		{
			var rows = await db.Messages
				.IgnoreQueryFilters()
				.Where(m => m.Body != "" && !m.Body.StartsWith(EncPrefix))
				.OrderBy(m => m.Id)
				.Take(batchSize)
				.ToListAsync(ct);

			if (rows.Count == 0) break;

			foreach (var m in rows)
				m.Body = protector.Protect(m.Body);

			await db.SaveChangesAsync(ct);
			db.ChangeTracker.Clear(); // bound memory across batches
			total += rows.Count;
		}

		if (total > 0)
			logger.LogInformation("Backfill: encrypted {Count} legacy message bodies at rest.", total);
	}

	/// <summary>Move legacy chat attachments (public bucket, no StorageKey) into the private bucket and
	/// repoint the row to a presigned URL. Verify-before-delete: copy → HEAD-verify on private → update
	/// DB → delete the public original. Per-row safe: any failure skips that row without deleting.</summary>
	public static async Task MigrateLegacyAttachmentsToPrivateAsync(
		WaaoDbContext db, R2Options opts, ILogger logger, CancellationToken ct = default)
	{
		if (!opts.IsConfigured || !opts.HasPrivateBucket) return;

		var legacy = await db.MessageAttachments
			.IgnoreQueryFilters()
			.Where(a => (a.StorageKey == null || a.StorageKey == "") && a.Url != "")
			.ToListAsync(ct);

		if (legacy.Count == 0) return;

		using var s3 = new AmazonS3Client(opts.AccessKey, opts.SecretKey, new AmazonS3Config
		{
			ServiceURL = opts.Endpoint,
			ForcePathStyle = true,
			AuthenticationRegion = "auto",
		});

		var migrated = 0;
		var failed = 0;

		foreach (var a in legacy)
		{
			var key = DeriveKeyFromUrl(a.Url);
			if (key is null)
			{
				logger.LogWarning("Attachment migration: could not derive key from URL {Url} (id={Id}) — skipped.", a.Url, a.Id);
				failed++;
				continue;
			}

			try
			{
				// 1. Copy public → private (also exercises the token's private-bucket write access).
				await s3.CopyObjectAsync(new CopyObjectRequest
				{
					SourceBucket = opts.Bucket,
					SourceKey = key,
					DestinationBucket = opts.PrivateBucketName,
					DestinationKey = key,
				}, ct);

				// 2. Verify the object is readable in the private bucket before touching anything else.
				await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
				{
					BucketName = opts.PrivateBucketName,
					Key = key,
				}, ct);

				// 3. Repoint the row to the private object (read path now presigns from StorageKey).
				a.StorageKey = key;
				a.Url = string.Empty;
				await db.SaveChangesAsync(ct);

				// 4. Only now delete the public original (best-effort — already private if this fails).
				try
				{
					await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = opts.Bucket, Key = key }, ct);
				}
				catch (Exception ex)
				{
					logger.LogWarning(ex, "Attachment migration: private copy OK but failed to delete public original {Key} — orphaned (still public).", key);
				}

				migrated++;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Attachment migration failed for key={Key} (id={Id}) — left on public bucket. If this is AccessDenied, grant the R2 token access to {PrivateBucket}.", key, a.Id, opts.PrivateBucketName);
				failed++;
			}
		}

		logger.LogInformation("Backfill: migrated {Migrated} legacy attachments to private bucket {Bucket} ({Failed} failed/skipped).",
			migrated, opts.PrivateBucketName, failed);
	}

	/// <summary>Chat attachment keys always start with <c>waao/chat/</c>, so we can recover the object key
	/// from any stored public URL regardless of the public-base-URL form.</summary>
	private static string? DeriveKeyFromUrl(string url)
	{
		var idx = url.IndexOf("waao/chat/", StringComparison.Ordinal);
		return idx >= 0 ? url[idx..] : null;
	}
}
