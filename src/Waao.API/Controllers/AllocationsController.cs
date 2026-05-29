using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/allocations")]
[Authorize]
public class AllocationsController(IAllocationService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("board")]
	[ProducesResponseType(typeof(AllocationBoardDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetBoard(CancellationToken ct)
		=> Ok(await Service.GetBoardAsync(ct));

	[HttpGet("by-collaborator/{id:guid}")]
	[ProducesResponseType(typeof(IReadOnlyList<ProjectWithAllocationsDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByCollaborator(Guid id, CancellationToken ct)
		=> Ok(await Service.GetByCollaboratorAsync(id, ct));

	// ----- Project config (admin) -----
	[HttpPost("projects")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(ProjectWithAllocationsDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateProjectAsync(dto, ct));

	[HttpPut("projects/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(ProjectWithAllocationsDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateProjectAsync(id, dto, ct));

	[HttpPut("projects/reorder")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ReorderProjects([FromBody] ReorderProjectsDto dto, CancellationToken ct)
	{
		await Service.ReorderProjectsAsync(dto, ct);
		return NoContent();
	}

	[HttpDelete("projects/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ArchiveProject(Guid id, CancellationToken ct)
	{
		await Service.ArchiveProjectAsync(id, ct);
		return NoContent();
	}

	// ----- Allocations (any collaborator) -----
	[HttpPost]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Allocate([FromBody] CreateAllocationDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.AllocateAsync(dto, Me, ct));

	[HttpPut("{id:guid}/move")]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Move(Guid id, [FromBody] MoveAllocationDto dto, CancellationToken ct)
		=> Ok(await Service.MoveAllocationAsync(id, dto, ct));

	[HttpPut("{id:guid}/note")]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateNoteAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
	{
		await Service.RemoveAllocationAsync(id, ct);
		return NoContent();
	}
}
