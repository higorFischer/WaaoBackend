using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/admin")]
[Authorize(Policy = "Admin")]
public class AdminController(IAdminService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	// ----- People -----
	[HttpPost("collaborators/{id:guid}/promote")]
	public async Task<IActionResult> Promote(Guid id, [FromBody] PromoteCollaboratorDto dto, CancellationToken ct)
		=> Ok(await Service.PromoteAsync(id, dto, Me, ct));

	[HttpPatch("collaborators/{id:guid}/role-kind")]
	public async Task<IActionResult> SetRoleKind(Guid id, [FromBody] SetRoleKindDto dto, CancellationToken ct)
		=> Ok(await Service.SetRoleKindAsync(id, dto, Me, ct));

	[HttpPatch("collaborators/{id:guid}/role")]
	public async Task<IActionResult> SetRole(Guid id, [FromBody] SetCollaboratorRoleDto dto, CancellationToken ct)
		=> Ok(await Service.SetCollaboratorRoleAsync(id, dto, Me, ct));

	// ----- Job roles -----
	[HttpGet("roles")]
	public async Task<IActionResult> ListRoles(CancellationToken ct)
		=> Ok(await Service.ListJobRolesAsync(ct));

	[HttpPost("roles")]
	public async Task<IActionResult> CreateRole([FromBody] CreateJobRoleDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateJobRoleAsync(dto, ct));

	[HttpPut("roles/{id:guid}")]
	public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateJobRoleDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateJobRoleAsync(id, dto, ct));

	[HttpDelete("roles/{id:guid}")]
	public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
	{
		await Service.DeleteJobRoleAsync(id, ct);
		return NoContent();
	}

	// ----- Departments -----
	[HttpGet("departments")]
	public async Task<IActionResult> ListDepartments(CancellationToken ct)
		=> Ok(await Service.ListDepartmentsAsync(ct));

	[HttpPost("departments")]
	public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateDepartmentAsync(dto, ct));

	[HttpPut("departments/{id:guid}")]
	public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateDepartmentAsync(id, dto, ct));

	[HttpDelete("departments/{id:guid}")]
	public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken ct)
	{
		await Service.DeleteDepartmentAsync(id, ct);
		return NoContent();
	}

	// ----- Levels -----
	[HttpGet("levels")]
	public async Task<IActionResult> ListLevels(CancellationToken ct)
		=> Ok(await Service.ListLevelsAsync(ct));

	[HttpPost("levels")]
	public async Task<IActionResult> UpsertLevel([FromBody] UpsertLevelDefinitionDto dto, CancellationToken ct)
		=> Ok(await Service.UpsertLevelAsync(dto, ct));

	[HttpDelete("levels/{id:guid}")]
	public async Task<IActionResult> DeleteLevel(Guid id, CancellationToken ct)
	{
		await Service.DeleteLevelAsync(id, ct);
		return NoContent();
	}
}

/// <summary>
/// Read-only catalog endpoints for any authenticated user — used by selects across the app.
/// </summary>
[ApiController]
[Route("api/waao/catalog")]
[Authorize]
public class CatalogController(IAdminService Service) : ControllerBase
{
	[HttpGet("roles")]
	public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await Service.ListJobRolesAsync(ct));

	[HttpGet("departments")]
	public async Task<IActionResult> Departments(CancellationToken ct) => Ok(await Service.ListDepartmentsAsync(ct));

	[HttpGet("levels")]
	public async Task<IActionResult> Levels(CancellationToken ct) => Ok(await Service.ListLevelsAsync(ct));
}
