using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/collaborators")]
[Authorize]
public class CollaboratorsController(ICollaboratorService Service) : ControllerBase
{
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CollaboratorDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAll(CancellationToken ct)
		=> Ok(await Service.GetAllAsync(ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
	{
		var dto = await Service.GetByIdAsync(id, ct);
		return dto is null ? NotFound() : Ok(dto);
	}

	[HttpPost]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateCollaboratorDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCollaboratorDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, ct);
		return NoContent();
	}
}
