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

	[HttpGet("")]
	[ProducesResponseType(typeof(IReadOnlyList<ChannelDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMine(CancellationToken ct)
		=> Ok(await ChannelService.ListMyChannelsAsync(Me, ct));

	[HttpGet("public")]
	[ProducesResponseType(typeof(IReadOnlyList<ChannelDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetPublic(CancellationToken ct)
		=> Ok(await ChannelService.ListPublicChannelsAsync(Me, ct));

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
		var kind = mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
			? MessageAttachmentKind.Image
			: mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
				? MessageAttachmentKind.Audio
				: MessageAttachmentKind.File;

		// Key: chat/<channelId>/<yyyyMMdd>/<guid>-<sanitized-name>
		var safeName = System.IO.Path.GetFileName(file.FileName ?? "file");
		safeName = string.Concat(safeName.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_'));
		if (string.IsNullOrEmpty(safeName)) safeName = "file";
		var key = $"waao/chat/{id:N}/{DateTime.UtcNow:yyyyMMdd}/{Guid.CreateVersion7():N}-{safeName}";

		try
		{
			using var stream = file.OpenReadStream();
			var url = await Storage.UploadAsync(key, stream, mime, ct);

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
