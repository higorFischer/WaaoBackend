using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/auth")]
public class AuthController(IAuthService Service) : ControllerBase
{
	[HttpPost("login")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
		=> Ok(await Service.LoginAsync(dto, ct));

	[HttpPost("register")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.RegisterAsync(dto, ct));

	[HttpGet("me")]
	[Authorize]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Me(CancellationToken ct)
	{
		var id = CurrentCollaboratorId();
		var dto = await Service.GetMeAsync(id, ct);
		return dto is null ? NotFound() : Ok(dto);
	}

	[HttpPost("change-password")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
	{
		await Service.ChangePasswordAsync(CurrentCollaboratorId(), dto, ct);
		return NoContent();
	}

	private Guid CurrentCollaboratorId()
	{
		var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
		return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing subject claim.");
	}
}
