using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Waao.API.Hubs;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/channels")]
[Authorize]
public class ChannelsController(
	IChannelService ChannelService,
	IMessageService MessageService,
	IHubContext<MessagingHub> Hub) : ControllerBase
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
}

// Small inline DTO for adding a member (avoids a dedicated file for a single property)
public record AddMemberDto
{
	public Guid CollaboratorId { get; init; }
}
