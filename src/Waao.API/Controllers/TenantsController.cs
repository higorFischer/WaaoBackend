using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/tenants")]
public class TenantsController(ITenantService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	/// <summary>Public — used by the login picker when an email belongs to multiple tenants.</summary>
	[HttpGet("for-email")]
	[ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListForEmail([FromQuery] string email, CancellationToken ct)
		=> Ok(await Service.ListForEmailAsync(email, ct));

	/// <summary>The tenant carried in the caller's JWT.</summary>
	[HttpGet("current")]
	[Authorize]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetCurrent(CancellationToken ct)
	{
		var t = await Service.GetCurrentAsync(ct);
		return t is null ? NoContent() : Ok(t);
	}

	/// <summary>Re-issues the JWT for the caller acting inside another tenant.</summary>
	[HttpPost("switch")]
	[Authorize]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Switch([FromBody] SwitchTenantDto dto, CancellationToken ct)
		=> Ok(await Service.SwitchAsync(Me, dto.TenantId, ct));

	/// <summary>Every tenant — admin/super-admin only.</summary>
	[HttpGet]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListAll(CancellationToken ct)
		=> Ok(await Service.ListAllAsync(ct));
}
