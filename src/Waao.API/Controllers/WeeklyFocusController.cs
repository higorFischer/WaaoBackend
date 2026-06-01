using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Focus;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/focus")]
[Authorize]
public class WeeklyFocusController(IWeeklyFocusService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	private bool IsAdmin => User.IsInRole("Admin");

	[HttpGet("current")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> GetCurrent(CancellationToken ct)
	{
		var dto = IsAdmin
			? await Service.GetCurrentForAdminAsync(ct)
			: await Service.GetCurrentPublishedAsync(ct);

		return dto is null ? NoContent() : Ok(dto);
	}

	[HttpGet]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(IReadOnlyList<WeeklyFocusDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken ct)
		=> Ok(await Service.ListAsync(ct));

	[HttpGet("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await Service.GetByIdAsync(id, ct));

	[HttpPost]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Create([FromBody] CreateWeeklyFocusDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWeeklyFocusDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	[HttpPost("{id:guid}/publish")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
		=> Ok(await Service.SetPublishedAsync(id, true, ct));

	[HttpPost("{id:guid}/unpublish")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
		=> Ok(await Service.SetPublishedAsync(id, false, ct));

	[HttpPost("{id:guid}/goals/{goalId:guid}/toggle")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(WeeklyFocusDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> ToggleGoal(Guid id, Guid goalId, CancellationToken ct)
		=> Ok(await Service.ToggleGoalAsync(id, goalId, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await Service.DeleteAsync(id, ct);
		return NoContent();
	}
}
