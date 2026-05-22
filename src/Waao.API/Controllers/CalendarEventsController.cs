using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/calendar/events")]
[Authorize]
public class CalendarEventsController(ICalendarEventService EventService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CalendarOccurrenceDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetOccurrences([FromQuery] EventWindowQueryDto query, CancellationToken ct)
		=> Ok(await EventService.GetOccurrencesAsync(query, Me, ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await EventService.GetEventAsync(id, Me, ct));

	[HttpPost]
	[ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateCalendarEventDto dto, CancellationToken ct)
		=> Created(string.Empty, await EventService.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(
		Guid id,
		[FromBody] UpdateCalendarEventDto dto,
		[FromQuery] string editScope = "all",
		[FromQuery] DateTime? originalStartUtc = null,
		CancellationToken ct = default)
		=> Ok(await EventService.UpdateAsync(id, dto, editScope, originalStartUtc, Me, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(
		Guid id,
		[FromQuery] string editScope = "all",
		[FromQuery] DateTime? originalStartUtc = null,
		CancellationToken ct = default)
	{
		await EventService.DeleteAsync(id, editScope, originalStartUtc, Me, ct);
		return NoContent();
	}
}
