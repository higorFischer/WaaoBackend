using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.OneOnOnes;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/one-on-ones")]
[Authorize]
public class OneOnOnesController(IOneOnOneService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<OneOnOneDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListMine(CancellationToken ct)
		=> Ok(await Service.ListMineAsync(Me, ct));

	[HttpGet("my-action-items")]
	[ProducesResponseType(typeof(IReadOnlyList<OneOnOneActionItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> MyActionItems(CancellationToken ct)
		=> Ok(await Service.ListMyOpenActionItemsAsync(Me, ct));

	[HttpGet("by-collaborator/{collaboratorId:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(IReadOnlyList<OneOnOneDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListForCollaborator(Guid collaboratorId, CancellationToken ct)
		=> Ok(await Service.ListForCollaboratorAsync(collaboratorId, ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await Service.GetByIdAsync(id, Me, ct));

	[HttpPost]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateOneOnOneDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOneOnOneDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, Me, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/action-items")]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> AddActionItem(Guid id, [FromBody] CreateActionItemDto dto, CancellationToken ct)
		=> Ok(await Service.AddActionItemAsync(id, dto, Me, ct));

	[HttpPost("{id:guid}/action-items/{itemId:guid}/toggle")]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> ToggleActionItem(Guid id, Guid itemId, CancellationToken ct)
		=> Ok(await Service.ToggleActionItemAsync(id, itemId, Me, ct));

	[HttpDelete("{id:guid}/action-items/{itemId:guid}")]
	[ProducesResponseType(typeof(OneOnOneDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> RemoveActionItem(Guid id, Guid itemId, CancellationToken ct)
		=> Ok(await Service.RemoveActionItemAsync(id, itemId, Me, ct));
}
