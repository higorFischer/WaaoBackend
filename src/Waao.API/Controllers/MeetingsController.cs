using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/meetings")]
[Authorize]
public class MeetingsController(IMeetingService MeetingService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpPost]
	[ProducesResponseType(typeof(MeetingDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto, CancellationToken ct)
		=> Created(string.Empty, await MeetingService.CreateAsync(dto, Me, ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await MeetingService.GetAsync(id, Me, ct));

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingDto dto, CancellationToken ct)
		=> Ok(await MeetingService.UpdateAsync(id, dto, Me, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
	{
		await MeetingService.CancelAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/rsvp")]
	[ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SetRsvp(Guid id, [FromBody] SetRsvpDto dto, CancellationToken ct)
		=> Ok(await MeetingService.SetRsvpAsync(id, dto, Me, ct));

	[HttpGet("mine")]
	[ProducesResponseType(typeof(IReadOnlyList<MeetingDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetMine([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct)
		=> Ok(await MeetingService.ListMyMeetingsAsync(Me, fromUtc, toUtc, ct));
}
