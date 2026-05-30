using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Kudos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/kudos")]
[Authorize]
public class KudosController(IKudosService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpPost]
	[ProducesResponseType(typeof(KudoDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Give([FromBody] GiveKudoDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.GiveAsync(dto, Me, ct));

	[HttpGet("feed")]
	[ProducesResponseType(typeof(KudoFeedDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFeed([FromQuery] Guid? before, [FromQuery] int limit = 30, CancellationToken ct = default)
		=> Ok(await Service.GetFeedAsync(before, limit, ct));

	[HttpGet("mine/received")]
	[ProducesResponseType(typeof(IReadOnlyList<KudoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetReceived(CancellationToken ct)
		=> Ok(await Service.GetReceivedAsync(Me, ct));

	[HttpGet("mine/given")]
	[ProducesResponseType(typeof(IReadOnlyList<KudoDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetGiven(CancellationToken ct)
		=> Ok(await Service.GetGivenAsync(Me, ct));
}
