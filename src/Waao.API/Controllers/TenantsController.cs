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

	/// <summary>Create a new tenant. Mirrors the calling admin into it as the first member.</summary>
	[HttpPost]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Create([FromBody] CreateTenantDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(Me, dto, ct));

	/// <summary>Update a tenant's display fields.</summary>
	[HttpPut("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));
}
