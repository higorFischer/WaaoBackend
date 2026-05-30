using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Badges;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/badges")]
[Authorize]
public class BadgesController(IBadgeAdminService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("flair/active")]
	[ProducesResponseType(typeof(IReadOnlyList<CollaboratorFlairDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetActiveFlair(CancellationToken ct)
		=> Ok(await Service.GetActiveFlairAsync(ct));

	[HttpGet("definitions")]
	[ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListDefinitions(CancellationToken ct)
		=> Ok(await Service.ListManualDefinitionsAsync(ct));

	[HttpPost("definitions")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(BadgeDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateDefinition([FromBody] CreateBadgeDefinitionDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateDefinitionAsync(dto, ct));

	[HttpPut("definitions/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(BadgeDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] UpdateBadgeDefinitionDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateDefinitionAsync(id, dto, ct));

	[HttpDelete("definitions/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken ct)
	{
		await Service.DeleteDefinitionAsync(id, ct);
		return NoContent();
	}

	[HttpGet("definitions/{id:guid}/grants")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(IReadOnlyList<FlairBadgeDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetGrantsForBadge(Guid id, CancellationToken ct)
		=> Ok(await Service.GetGrantsForBadgeAsync(id, ct));

	[HttpPost("grant")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(FlairBadgeDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Grant([FromBody] GrantBadgeDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.GrantAsync(dto, Me, ct));

	[HttpDelete("grant/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
	{
		await Service.RevokeAsync(id, ct);
		return NoContent();
	}
}
