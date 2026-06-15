using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Team;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao")]
[Authorize]
public class SkillsController(ISkillService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	// ----- Catalog -----
	[HttpGet("skills")]
	[ProducesResponseType(typeof(IReadOnlyList<SkillDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCatalog([FromQuery] bool includeArchived, CancellationToken ct)
		=> Ok(await Service.GetCatalogAsync(includeArchived, ct));

	[HttpPost("skills")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateSkillDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, ct));

	[HttpPut("skills/{id:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	[HttpDelete("skills/{id:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, ct);
		return NoContent();
	}

	// ----- Per-collaborator assessments -----
	[HttpGet("collaborators/{id:guid}/skills")]
	[ProducesResponseType(typeof(IReadOnlyList<CollaboratorSkillDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> GetForCollaborator(Guid id, CancellationToken ct)
		=> Ok(await Service.GetForCollaboratorAsync(id, Me, ct));

	[HttpPut("collaborators/{id:guid}/skills/{skillId:guid}")]
	[ProducesResponseType(typeof(CollaboratorSkillDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Upsert(Guid id, Guid skillId, [FromBody] UpsertCollaboratorSkillDto dto, CancellationToken ct)
		=> Ok(await Service.UpsertForCollaboratorAsync(id, skillId, dto, Me, ct));

	[HttpDelete("collaborators/{id:guid}/skills/{skillId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Remove(Guid id, Guid skillId, CancellationToken ct)
	{
		await Service.RemoveForCollaboratorAsync(id, skillId, Me, ct);
		return NoContent();
	}
}
