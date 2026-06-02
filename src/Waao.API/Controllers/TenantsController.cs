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
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListAll(CancellationToken ct)
		=> Ok(await Service.ListAllAsync(ct));

	/// <summary>Create a new tenant. Mirrors the calling admin into it as the first member.</summary>
	[HttpPost]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Create([FromBody] CreateTenantDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateAsync(Me, dto, ct));

	/// <summary>Update a tenant's display fields.</summary>
	[HttpPut("{id:guid}")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAsync(id, dto, ct));

	/// <summary>
	/// Mirror the calling admin into an existing tenant they don't yet belong to and return a
	/// JWT scoped to that tenant. Idempotent — re-joining an already-joined tenant just
	/// re-issues the token. Used to bootstrap empty tenants from the admin UI.
	/// </summary>
	[HttpPost("{id:guid}/join")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Join(Guid id, CancellationToken ct)
		=> Ok(await Service.JoinAsync(Me, id, ct));

	/// <summary>Email domains a tenant has allowlisted for self-registration.</summary>
	[HttpGet("{id:guid}/allowed-domains")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(IReadOnlyList<TenantAllowedDomainDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> ListAllowedDomains(Guid id, CancellationToken ct)
		=> Ok(await Service.ListAllowedDomainsAsync(id, ct));

	/// <summary>Allowlist a new email domain for the tenant.</summary>
	[HttpPost("{id:guid}/allowed-domains")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(TenantAllowedDomainDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> AddAllowedDomain(Guid id, [FromBody] AddAllowedDomainDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.AddAllowedDomainAsync(id, dto.Domain, ct));

	/// <summary>Removes an allowlisted domain by row id.</summary>
	[HttpDelete("allowed-domains/{domainId:guid}")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> RemoveAllowedDomain(Guid domainId, CancellationToken ct)
	{
		await Service.RemoveAllowedDomainAsync(domainId, ct);
		return NoContent();
	}

	/// <summary>
	/// Uploads a logo image to R2 and stores the public URL on the tenant. Shown in the sidebar
	/// brand block and the workspace switcher. Same constraints as the avatar uploader (8 MB,
	/// image/* MIME, public bucket so cross-origin reads work).
	/// </summary>
	[HttpPost("{id:guid}/logo")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
	[RequestSizeLimit(8_000_000)]
	public async Task<IActionResult> UploadLogo(
		Guid id,
		[FromForm] IFormFile file,
		[FromServices] Waao.Services.Abstractions.Services.IR2StorageService Storage,
		[FromServices] ILogger<TenantsController> Logger,
		CancellationToken ct)
	{
		if (file is null || file.Length == 0) return BadRequest("Empty file.");
		if (file.Length > 8_000_000) return StatusCode(StatusCodes.Status413PayloadTooLarge);
		var mime = file.ContentType ?? "application/octet-stream";
		if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
			return BadRequest("Logo must be an image.");
		if (!Storage.IsEnabled)
			return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage is not configured.");

		var ext = mime switch
		{
			"image/jpeg"   => "jpg",
			"image/png"    => "png",
			"image/webp"   => "webp",
			"image/svg+xml"=> "svg",
			"image/gif"    => "gif",
			_              => "bin",
		};
		var key = $"waao/tenant-logos/{id:N}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.CreateVersion7():N}.{ext}";
		try
		{
			using var stream = file.OpenReadStream();
			var url = await Storage.UploadAsync(key, stream, mime, ct);
			return Ok(await Service.SetLogoAsync(id, url, ct));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Tenant logo upload failed tenant={Tenant} fileName={Name} size={Size}", id, file.FileName, file.Length);
			return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
		}
	}

	/// <summary>Clears the tenant's logo (sets logo_url to null).</summary>
	[HttpDelete("{id:guid}/logo")]
	[Authorize(Policy = "SuperAdmin")]
	[ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> ClearLogo(Guid id, CancellationToken ct)
		=> Ok(await Service.SetLogoAsync(id, null, ct));
}
