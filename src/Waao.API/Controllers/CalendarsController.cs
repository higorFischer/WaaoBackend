using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/calendars")]
[Authorize]
public class CalendarsController(ICalendarService CalendarService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CalendarDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken ct)
		=> Ok(await CalendarService.ListVisibleCalendarsAsync(Me, ct));
}
