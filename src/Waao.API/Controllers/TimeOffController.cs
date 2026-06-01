using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.TimeOff;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/timeoff")]
[Authorize]
public class TimeOffController(ITimeOffService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpPost]
	[ProducesResponseType(typeof(TimeOffRequestDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateTimeOffDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.RequestAsync(dto, Me, ct));

	[HttpGet("mine")]
	[ProducesResponseType(typeof(IReadOnlyList<TimeOffRequestDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListMine(CancellationToken ct)
		=> Ok(await Service.ListMineAsync(Me, ct));

	[HttpGet("pending")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(IReadOnlyList<TimeOffRequestDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListPending(CancellationToken ct)
		=> Ok(await Service.ListPendingAsync(ct));

	[HttpGet("calendar")]
	[ProducesResponseType(typeof(IReadOnlyList<TimeOffAbsenceDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCalendar([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
		=> Ok(await Service.GetAbsencesAsync(from, to, ct));

	[HttpPost("{id:guid}/approve")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(TimeOffRequestDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewTimeOffDto dto, CancellationToken ct)
		=> Ok(await Service.ReviewAsync(id, true, dto, Me, ct));

	[HttpPost("{id:guid}/reject")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(TimeOffRequestDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewTimeOffDto dto, CancellationToken ct)
		=> Ok(await Service.ReviewAsync(id, false, dto, Me, ct));

	[HttpPost("{id:guid}/cancel")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
	{
		await Service.CancelAsync(id, Me, ct);
		return NoContent();
	}

	[HttpGet("balance")]
	[ProducesResponseType(typeof(TimeOffBalanceDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMyBalance([FromQuery] int? year, CancellationToken ct)
		=> Ok(await Service.GetBalanceAsync(Me, year ?? DateTime.UtcNow.Year, ct));

	[HttpGet("overlap")]
	[ProducesResponseType(typeof(IReadOnlyList<TimeOffOverlapDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetOverlap([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? exclude, CancellationToken ct)
		=> Ok(await Service.GetOverlapsAsync(from, to, exclude, ct));

	[HttpGet("by-collaborator/{collaboratorId:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(IReadOnlyList<TimeOffRequestDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListForCollaborator(Guid collaboratorId, CancellationToken ct)
		=> Ok(await Service.ListForCollaboratorAsync(collaboratorId, ct));
}
