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
	[ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.RegisterAsync(dto, ct));

	[HttpPost("verify-email")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto, CancellationToken ct)
		=> Ok(await Service.VerifyEmailAsync(dto, ct));

	// Block body (not expression-bodied): the service returns void and the response is a fixed always-200 body — no controller logic, by design.
	[HttpPost("resend-verification")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto, CancellationToken ct)
	{
		await Service.ResendVerificationAsync(dto, ct);
		return Ok(new { status = "ok" });
	}

	[HttpGet("me")]
	[Authorize]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Me(CancellationToken ct)
	{
		var id = CurrentCollaboratorId();
		var dto = await Service.GetMeAsync(id, ct);
		return dto is null ? NotFound() : Ok(dto);
	}

	[HttpPost("refresh")]
	[Authorize]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> Refresh(CancellationToken ct)
		=> Ok(await Service.RefreshAsync(CurrentCollaboratorId(), ct));

	[HttpPost("change-password")]
	[Authorize]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
	{
		await Service.ChangePasswordAsync(CurrentCollaboratorId(), dto, ct);
		return NoContent();
	}

	[HttpPut("me/profile")]
	[Authorize]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateMyProfileAsync(CurrentCollaboratorId(), dto, ct));

	public record SetDesktopNotificationsDto(bool Enabled);

	/// <summary>Persists the user's desktop-notification preference so a new
	/// device knows to auto-prompt the browser permission instead of forcing
	/// them through Settings again.</summary>
	[HttpPut("me/desktop-notifications")]
	[Authorize]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> SetDesktopNotifications([FromBody] SetDesktopNotificationsDto dto, CancellationToken ct)
		=> Ok(await Service.SetDesktopNotificationsEnabledAsync(CurrentCollaboratorId(), dto.Enabled, ct));

	[HttpPost("me/avatar")]
	[Authorize]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
	[RequestSizeLimit(8_000_000)]
	public async Task<IActionResult> UploadAvatar(
		[FromForm] IFormFile file,
		[FromServices] Waao.Services.Abstractions.Services.IR2StorageService Storage,
		[FromServices] ILogger<AuthController> Logger,
		CancellationToken ct)
	{
		if (file is null || file.Length == 0)
			return BadRequest("Empty file.");
		if (file.Length > 8_000_000)
			return StatusCode(StatusCodes.Status413PayloadTooLarge);
		var mime = file.ContentType ?? "application/octet-stream";
		if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
			return BadRequest("Avatar must be an image.");
		if (!Storage.IsEnabled)
			return StatusCode(StatusCodes.Status503ServiceUnavailable, "Storage is not configured.");

		var ext = mime switch
		{
			"image/jpeg" => "jpg",
			"image/png"  => "png",
			"image/webp" => "webp",
			"image/heic" => "heic",
			"image/gif"  => "gif",
			_            => "bin",
		};
		var me = CurrentCollaboratorId();
		var key = $"waao/avatars/{me:N}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.CreateVersion7():N}.{ext}";

		try
		{
			using var stream = file.OpenReadStream();
			var url = await Storage.UploadAsync(key, stream, mime, ct);
			return Ok(await Service.UpdateMyPhotoAsync(me, url, ct));
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Avatar upload failed user={User} fileName={Name} size={Size}", me, file.FileName, file.Length);
			return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
		}
	}

	private Guid CurrentCollaboratorId()
	{
		var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
		return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Missing subject claim.");
	}
}
