using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Waao.Services.Abstractions.Dtos.Meetings;
using Waao.Services.Abstractions.Services;
using Waao.Services.Transcription;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/meetings")]
[Authorize]
public class MeetingsController(
	IMeetingService MeetingService,
	IMeetingTranscriptService TranscriptService,
	IOptions<TranscriptionOptions> TranscriptionOptions) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpPost]
	[ProducesResponseType(typeof(MeetingDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateMeetingDto dto, CancellationToken ct)
		=> Created(string.Empty, await MeetingService.CreateAsync(dto, Me, ct));

	[HttpGet("transcripts")]
	[ProducesResponseType(typeof(IReadOnlyList<MeetingTranscriptSummaryDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListTranscripts(CancellationToken ct)
		=> Ok(await TranscriptService.ListMineAsync(Me, ct));

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

	[HttpPost("{id:guid}/end")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> End(Guid id, CancellationToken ct)
	{
		await MeetingService.EndAsync(id, Me, ct);
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

	[HttpGet("{id:guid}/video-token")]
	[ProducesResponseType(typeof(MeetingVideoTokenDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetVideoToken(Guid id, CancellationToken ct)
		=> Ok(await MeetingService.GetVideoTokenAsync(id, Me, ct));

	[HttpGet("{id:guid}/guest-link")]
	[ProducesResponseType(typeof(GuestLinkDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetGuestLink(Guid id, CancellationToken ct)
		=> Ok(await MeetingService.GetGuestLinkAsync(id, Me, ct));

	[HttpPost("{id:guid}/guest/join")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(GuestJoinResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GuestJoin(Guid id, [FromBody] GuestJoinRequestDto dto, CancellationToken ct)
		=> Ok(await MeetingService.JoinAsGuestAsync(id, dto, ct));

	[HttpGet("{id:guid}/transcription-enabled")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(TranscriptionEnabledDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetTranscriptionEnabled(Guid id, CancellationToken ct)
	{
		var key = Request.Headers["X-Transcription-Key"].FirstOrDefault();
		if (string.IsNullOrWhiteSpace(key) || key != TranscriptionOptions.Value.IngestKey)
			return Unauthorized();
		return Ok(await MeetingService.GetTranscriptionEnabledAsync(id, ct));
	}

	[HttpPost("{id:guid}/transcript")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> IngestTranscript(Guid id, [FromBody] IngestTranscriptDto dto, CancellationToken ct)
	{
		var key = Request.Headers["X-Transcription-Key"].FirstOrDefault();
		if (string.IsNullOrWhiteSpace(key) || key != TranscriptionOptions.Value.IngestKey)
			return Unauthorized();
		await TranscriptService.IngestAsync(id, dto, ct);
		return NoContent();
	}

	[HttpGet("{id:guid}/transcript")]
	[ProducesResponseType(typeof(MeetingTranscriptDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetTranscript(Guid id, CancellationToken ct)
		=> Ok(await TranscriptService.GetAsync(id, Me, ct));
}
