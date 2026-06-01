using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Waao.API.Hubs;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/channels")]
[Authorize]
public class ChannelsController(
	IChannelService ChannelService,
	IMessageService MessageService,
	IR2StorageService Storage,
	IHubContext<MessagingHub> Hub,
	ILogger<ChannelsController> Logger) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	/// <summary>Validity window for presigned attachment URLs returned on upload (re-signed on read).</summary>
	private static readonly TimeSpan AttachmentUrlTtl = TimeSpan.FromHours(12);

	[HttpGet("")]
	[ProducesResponseType(typeof(IReadOnlyList<ChannelDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMine(CancellationToken ct)
		=> Ok(await ChannelService.ListMyChannelsAsync(Me, ct));

	[HttpGet("public")]
	[ProducesResponseType(typeof(IReadOnlyList<ChannelDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetPublic(CancellationToken ct)
		=> Ok(await ChannelService.ListPublicChannelsAsync(Me, ct));

	[HttpGet("messages/recent")]
	[ProducesResponseType(typeof(IReadOnlyList<RecentMessageDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetRecentAcrossMyChannels([FromQuery] int limit = 10, CancellationToken ct = default)
		=> Ok(await MessageService.GetRecentAcrossMyChannelsAsync(Me, limit, ct));

	[HttpPost("")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateChannelDto dto, CancellationToken ct)
		=> Created(string.Empty, await ChannelService.CreateChannelAsync(dto, Me, ct));

	[HttpPost("{id:guid}/join")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Join(Guid id, CancellationToken ct)
		=> Ok(await ChannelService.JoinAsync(id, Me, ct));

	[HttpPost("{id:guid}/leave")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
	{
		await ChannelService.LeaveAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/members")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberDto dto, CancellationToken ct)
		=> Ok(await ChannelService.AddMemberAsync(id, dto.CollaboratorId, Me, ct));

	[HttpDelete("{id:guid}/members/{collaboratorId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> RemoveMember(Guid id, Guid collaboratorId, CancellationToken ct)
	{
		await ChannelService.RemoveMemberAsync(id, collaboratorId, Me, ct);
		return NoContent();
	}

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChannelDto dto, CancellationToken ct)
		=> Ok(await ChannelService.UpdateChannelAsync(id, dto, Me, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await ChannelService.DeleteChannelAsync(id, Me, ct);
		return NoContent();
	}

	[HttpGet("{id:guid}/members")]
	[ProducesResponseType(typeof(IReadOnlyList<ChannelMemberDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
		=> Ok(await ChannelService.GetMembersAsync(id, Me, ct));

	[HttpPost("{id:guid}/read")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> MarkRead(Guid id, [FromBody] MarkReadDto dto, CancellationToken ct)
	{
		await ChannelService.MarkReadAsync(id, dto, Me, ct);
		return NoContent();
	}

	[HttpPut("{id:guid}/mute")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SetMuted(Guid id, [FromBody] SetMutedDto dto, CancellationToken ct)
		=> Ok(await ChannelService.SetMutedAsync(id, Me, dto.Muted, ct));

	[HttpGet("{id:guid}/messages")]
	[ProducesResponseType(typeof(MessagePageDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetMessages(
		Guid id,
		[FromQuery] Guid? before,
		[FromQuery] int limit = 50,
		CancellationToken ct = default)
		=> Ok(await MessageService.GetMessagesAsync(id, Me, before, limit, ct));

	[HttpPost("{id:guid}/messages")]
	[ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> PostMessage(Guid id, [FromBody] PostMessageDto dto, CancellationToken ct)
	{
		var message = await MessageService.PostMessageAsync(id, dto, Me, ct);
		await Hub.Clients.Group(MessagingHub.GroupName(id)).SendAsync("messageReceived", message, ct);
		return Created(string.Empty, message);
	}

	[HttpPut("{id:guid}/messages/{messageId:guid}")]
	[ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> EditMessage(Guid id, Guid messageId, [FromBody] EditMessageDto dto, CancellationToken ct)
	{
		var message = await MessageService.EditMessageAsync(id, messageId, dto, Me, ct);
		await Hub.Clients.Group(MessagingHub.GroupName(id)).SendAsync("messageEdited", message, ct);
		return Ok(message);
	}

	/// <summary>
	/// Toggles a single emoji reaction by the caller on a message (WhatsApp-style). Returns the
	/// updated reactions summary and broadcasts <c>messageReactionUpdated</c> to the channel
	/// group so every connected client patches the message in place.
	/// </summary>
	[HttpPost("{id:guid}/messages/{messageId:guid}/reactions")]
	[ProducesResponseType(typeof(MessageReactionUpdatedDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ToggleReaction(Guid id, Guid messageId, [FromBody] ReactionTogglePayloadDto dto, CancellationToken ct)
	{
		var result = await MessageService.ToggleReactionAsync(messageId, dto.Emoji, Me, ct);
		await Hub.Clients.Group(MessagingHub.GroupName(id)).SendAsync("messageReactionUpdated", result, ct);
		return Ok(result);
	}

	[HttpPost("{id:guid}/attachments")]
	[ProducesResponseType(typeof(UploadedAttachmentDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
	[RequestSizeLimit(25_000_000)]
	public async Task<IActionResult> UploadAttachment(
		Guid id,
		[FromForm] IFormFile file,
		[FromForm] int? durationSeconds,
		CancellationToken ct)
	{
		if (file is null || file.Length == 0)
			return BadRequest("Empty file.");
		if (file.Length > 25_000_000)
			return StatusCode(StatusCodes.Status413PayloadTooLarge);
		if (!Storage.IsEnabled)
			return StatusCode(StatusCodes.Status503ServiceUnavailable, "Attachments are not configured.");

		var mime = file.ContentType ?? "application/octet-stream";
		var ext = System.IO.Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();

		// Browsers occasionally drop the MIME (drag-drop from some apps, paste
		// from clipboard, generic application/octet-stream) — fall back to the
		// extension so an iPhone photo dropped in still renders as an image,
		// not as a file icon the user has to click.
		bool looksLikeImage = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
			|| ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp"
				or ".heic" or ".heif" or ".avif" or ".svg";
		bool looksLikeAudio = mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
			|| ext is ".mp3" or ".m4a" or ".wav" or ".ogg" or ".aac" or ".flac" or ".opus";

		// If the MIME was unknown but the extension says image, promote the
		// MIME too so the <img> tag actually loads (R2 serves whatever we set).
		if (looksLikeImage && !mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
		{
			mime = ext switch
			{
				".jpg" or ".jpeg" => "image/jpeg",
				".png" => "image/png",
				".gif" => "image/gif",
				".webp" => "image/webp",
				".heic" or ".heif" => "image/heic",
				".avif" => "image/avif",
				".bmp" => "image/bmp",
				".svg" => "image/svg+xml",
				_ => mime,
			};
		}

		var kind = looksLikeImage
			? MessageAttachmentKind.Image
			: looksLikeAudio
				? MessageAttachmentKind.Audio
				: MessageAttachmentKind.File;

		// Key: chat/<channelId>/<yyyyMMdd>/<guid>-<sanitized-name>
		var safeName = System.IO.Path.GetFileName(file.FileName ?? "file");
		safeName = string.Concat(safeName.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_'));
		if (string.IsNullOrEmpty(safeName)) safeName = "file";
		var key = $"waao/chat/{id:N}/{DateTime.UtcNow:yyyyMMdd}/{Guid.CreateVersion7():N}-{safeName}";

		try
		{
			// Prefer the PRIVATE bucket (served via short-lived presigned URLs). If it isn't configured,
			// or the R2 token can't reach it, fall back to the PUBLIC bucket so messaging never breaks —
			// the fallback is logged so the misconfiguration is visible.
			if (Storage.HasPrivateBucket)
			{
				try
				{
					using var stream = file.OpenReadStream();
					var storageKey = await Storage.UploadPrivateAsync(key, stream, mime, ct);
					return Ok(new UploadedAttachmentDto
					{
						Kind            = kind,
						Url             = Storage.GetPresignedUrl(storageKey, AttachmentUrlTtl),
						StorageKey      = storageKey,
						Mime            = mime,
						OriginalName    = file.FileName ?? safeName,
						SizeBytes       = file.Length,
						DurationSeconds = durationSeconds,
					});
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "Private bucket upload failed for key={Key} — falling back to PUBLIC bucket. Grant the R2 token access to the private bucket.", key);
				}
			}

			using var publicStream = file.OpenReadStream();
			var url = await Storage.UploadAsync(key, publicStream, mime, ct);
			return Ok(new UploadedAttachmentDto
			{
				Kind            = kind,
				Url             = url,
				Mime            = mime,
				OriginalName    = file.FileName ?? safeName,
				SizeBytes       = file.Length,
				DurationSeconds = durationSeconds,
			});
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Attachment upload failed channel={Channel} fileName={Name} size={Size} mime={Mime}",
				id, file.FileName, file.Length, mime);
			return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
		}
	}
}

// Small inline DTO for adding a member (avoids a dedicated file for a single property)
public record AddMemberDto
{
	public Guid CollaboratorId { get; init; }
}
