using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/career-events")]
[Authorize]
public class CareerEventsController(ICareerEventService Service) : ControllerBase
{
	[HttpGet("by-collaborator/{collaboratorId:guid}")]
	[ProducesResponseType(typeof(IReadOnlyList<CareerEventDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetForCollaborator(Guid collaboratorId, CancellationToken ct)
		=> Ok(await Service.GetForCollaboratorAsync(collaboratorId, ct));

	[HttpPost]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CareerEventCreatedDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateCareerEventDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, ct);
		return NoContent();
	}
}
