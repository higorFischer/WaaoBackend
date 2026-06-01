using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Announcements;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/announcements")]
[Authorize]
public class AnnouncementsController(IAnnouncementService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("active")]
	[ProducesResponseType(typeof(IReadOnlyList<AnnouncementDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListActiveForMe(CancellationToken ct)
		=> Ok(await Service.ListActiveForMeAsync(Me, ct));

	[HttpGet]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(IReadOnlyList<AnnouncementDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListAll(CancellationToken ct)
		=> Ok(await Service.ListAllAsync(ct));

	[HttpPost]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateAnnouncementDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAnnouncementDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
	{
		await Service.ArchiveAsync(id, ct);
		return NoContent();
	}
}
