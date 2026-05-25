using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.FeatureRequests;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/feature-requests")]
[Authorize]
public class FeatureRequestsController(IFeatureRequestService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("")]
	[ProducesResponseType(typeof(IReadOnlyList<FeatureRequestDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken ct)
		=> Ok(await Service.ListAsync(Me, ct));

	[HttpPost("")]
	[ProducesResponseType(typeof(FeatureRequestDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateFeatureRequestDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(dto, Me, ct));

	[HttpPost("{id:guid}/upvote")]
	[ProducesResponseType(typeof(FeatureRequestDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ToggleUpvote(Guid id, CancellationToken ct)
		=> Ok(await Service.ToggleUpvoteAsync(id, Me, ct));

	[HttpPut("{id:guid}/status")]
	[ProducesResponseType(typeof(FeatureRequestDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateFeatureRequestStatusDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateStatusAsync(id, dto, Me, ct));
}
