using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/gamification")]
[Authorize]
public class GamificationController(IGamificationService Service) : ControllerBase
{
	[HttpGet("collaborators/{id:guid}/progress")]
	[ProducesResponseType(typeof(LevelProgressDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetProgress(Guid id, CancellationToken ct)
		=> Ok(await Service.GetLevelProgressAsync(id, ct));

	[HttpGet("collaborators/{id:guid}/badges")]
	[ProducesResponseType(typeof(IReadOnlyList<CollaboratorBadgeDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetBadges(Guid id, CancellationToken ct)
		=> Ok(await Service.GetBadgesAsync(id, ct));

	[HttpGet("collaborators/{id:guid}/xp-history")]
	[ProducesResponseType(typeof(IReadOnlyList<XpTransactionDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetXpHistory(Guid id, [FromQuery] int take = 50, CancellationToken ct = default)
		=> Ok(await Service.GetXpHistoryAsync(id, take, ct));

	[HttpGet("leaderboard")]
	[ProducesResponseType(typeof(IReadOnlyList<LeaderboardEntryDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetLeaderboard([FromQuery] int take = 20, CancellationToken ct = default)
		=> Ok(await Service.GetLeaderboardAsync(take, ct));

	[HttpGet("badges")]
	[ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAllBadges(CancellationToken ct)
		=> Ok(await Service.GetAllBadgesAsync(ct));
}
