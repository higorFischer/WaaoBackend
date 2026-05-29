using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Notifications;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/push")]
[Authorize]
public class PushController(IPushNotificationService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("vapid-public-key")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public IActionResult GetVapidPublicKey()
		=> Ok(new { publicKey = Service.PublicKey });

	[HttpPost("subscribe")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Subscribe([FromBody] SavePushSubscriptionDto dto, CancellationToken ct)
	{
		await Service.SaveSubscriptionAsync(Me, dto, Request.Headers.UserAgent.ToString(), ct);
		return NoContent();
	}

	[HttpPost("unsubscribe")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeDto dto, CancellationToken ct)
	{
		await Service.RemoveSubscriptionAsync(dto.Endpoint, ct);
		return NoContent();
	}
}
