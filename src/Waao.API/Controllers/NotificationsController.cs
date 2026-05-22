using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/notifications")]
[Authorize]
public class NotificationsController(INotificationService NotificationService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("")]
	[ProducesResponseType(typeof(NotificationListDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
		=> Ok(await NotificationService.ListAsync(Me, unreadOnly, ct));

	[HttpGet("unread-count")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
	{
		var list = await NotificationService.ListAsync(Me, unreadOnly: false, ct);
		return Ok(new { count = list.UnreadCount });
	}

	[HttpPost("{id:guid}/read")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
	{
		await NotificationService.MarkReadAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("read-all")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> MarkAllRead(CancellationToken ct)
	{
		await NotificationService.MarkAllReadAsync(Me, ct);
		return NoContent();
	}
}
