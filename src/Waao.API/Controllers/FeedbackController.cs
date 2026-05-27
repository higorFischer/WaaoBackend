using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Feedback;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/feedback")]
[Authorize]
public class FeedbackController(IFeedbackService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	/// <summary>Admin-only inbox. Optional ?status=New filter.</summary>
	[HttpGet("")]
	[ProducesResponseType(typeof(IReadOnlyList<FeedbackDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<IActionResult> List([FromQuery] FeedbackStatus? status, CancellationToken ct)
		=> Ok(await Service.ListAsync(Me, status, ct));

	/// <summary>Any authenticated collaborator can submit feedback.</summary>
	[HttpPost("")]
	[ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateFeedbackDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}/status")]
	[ProducesResponseType(typeof(FeedbackDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFeedbackStatusDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateStatusAsync(id, dto, Me, ct));
}
