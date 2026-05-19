using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/onboarding")]
[Authorize]
public class OnboardingController(IOnboardingService Service) : ControllerBase
{
	[HttpGet("status")]
	[ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GetStatus(CancellationToken ct)
		=> Ok(await Service.GetStatusAsync(CurrentCollaboratorId(), ct));

	[HttpPost("complete")]
	[ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Complete([FromBody] CompleteOnboardingDto dto, CancellationToken ct)
		=> Ok(await Service.CompleteAsync(CurrentCollaboratorId(), dto, ct));

	private Guid CurrentCollaboratorId()
	{
		var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
		return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing subject claim.");
	}
}
