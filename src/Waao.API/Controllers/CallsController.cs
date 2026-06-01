using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Calls;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/calls")]
[Authorize]
public class CallsController(ICallChannelService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CallChannelDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken ct)
		=> Ok(await Service.ListAsync(ct));

	[HttpPost]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(CallChannelDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateCallChannelDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(CallChannelDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCallChannelDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
	{
		await Service.ArchiveAsync(id, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/token")]
	[ProducesResponseType(typeof(CallTokenDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetToken(Guid id, CancellationToken ct)
		=> Ok(await Service.GetTokenAsync(id, Me, ct));
}
