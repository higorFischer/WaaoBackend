using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Messaging;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/dm")]
[Authorize]
public class DirectMessagesController(IChannelService ChannelService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpPost("{collaboratorId:guid}")]
	[ProducesResponseType(typeof(ChannelDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> OpenDm(Guid collaboratorId, CancellationToken ct)
		=> Ok(await ChannelService.OpenDirectMessageAsync(collaboratorId, Me, ct));
}
