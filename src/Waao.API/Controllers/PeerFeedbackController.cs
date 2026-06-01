using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Feedback;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/peer-feedback")]
[Authorize]
public class PeerFeedbackController(IPeerFeedbackService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	private bool IsStaff => User.IsInRole("Admin") || User.IsInRole("HR");

	[HttpPost]
	[ProducesResponseType(typeof(PeerFeedbackDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Give([FromBody] GivePeerFeedbackDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.GiveAsync(dto, Me, ct));

	[HttpGet("by-collaborator/{collaboratorId:guid}/received")]
	[ProducesResponseType(typeof(IReadOnlyList<PeerFeedbackDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListReceived(Guid collaboratorId, CancellationToken ct)
	{
		// Only the recipient themselves and staff can see another person's received feedback.
		if (collaboratorId != Me && !IsStaff)
			return Forbid();
		return Ok(await Service.ListReceivedAsync(collaboratorId, Me, IsStaff, ct));
	}

	[HttpGet("by-collaborator/{collaboratorId:guid}/given")]
	[ProducesResponseType(typeof(IReadOnlyList<PeerFeedbackDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListGiven(Guid collaboratorId, CancellationToken ct)
	{
		if (collaboratorId != Me)
			return Forbid();
		return Ok(await Service.ListGivenAsync(collaboratorId, Me, ct));
	}

	[HttpPost("{id:guid}/acknowledge")]
	[ProducesResponseType(typeof(PeerFeedbackDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
		=> Ok(await Service.AcknowledgeAsync(id, Me, ct));
}
