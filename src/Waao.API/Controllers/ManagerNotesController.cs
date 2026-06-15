using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Team;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao")]
[Authorize(Policy = "Admin")]
public class ManagerNotesController(IManagerNoteService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("collaborators/{id:guid}/manager-notes")]
	[ProducesResponseType(typeof(IReadOnlyList<ManagerNoteDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> GetForCollaborator(Guid id, CancellationToken ct)
		=> Ok(await Service.GetForCollaboratorAsync(id, Me, ct));

	[HttpPost("collaborators/{id:guid}/manager-notes")]
	[ProducesResponseType(typeof(ManagerNoteDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> Create(Guid id, [FromBody] CreateManagerNoteDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(id, dto, Me, ct));

	[HttpPut("manager-notes/{id:guid}")]
	[ProducesResponseType(typeof(ManagerNoteDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateManagerNoteDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, Me, ct));

	[HttpDelete("manager-notes/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, Me, ct);
		return NoContent();
	}
}
