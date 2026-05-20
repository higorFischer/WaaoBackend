using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Challenges;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/challenges")]
[Authorize]
public class ChallengesController(IChallengeService ChallengeService, IChallengeAttemptService AttemptService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	private bool IsAdminOrHr => User.IsInRole("Admin") || User.IsInRole("HR");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<ChallengeDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken ct)
		=> Ok(await ChallengeService.ListAsync(IsAdminOrHr, ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(ChallengeDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await ChallengeService.GetByIdAsync(id, IsAdminOrHr, ct));

	[HttpPost]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(ChallengeDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateChallengeDto dto, CancellationToken ct)
		=> Created(string.Empty, await ChallengeService.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(ChallengeDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateChallengeDto dto, CancellationToken ct)
		=> Ok(await ChallengeService.UpdateAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await ChallengeService.DeleteAsync(id, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/publish")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(ChallengeDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Publish(Guid id, [FromBody] PublishChallengeDto dto, CancellationToken ct)
		=> Ok(await ChallengeService.PublishAsync(id, dto.IsPublished, ct));

	[HttpPost("{id:guid}/questions")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(ChallengeQuestionDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> AddQuestion(Guid id, [FromBody] CreateChallengeQuestionDto dto, CancellationToken ct)
		=> Created(string.Empty, await ChallengeService.AddQuestionAsync(id, dto, ct));

	[HttpPut("{id:guid}/questions/{questionId:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(ChallengeQuestionDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateQuestion(Guid id, Guid questionId, [FromBody] UpdateChallengeQuestionDto dto, CancellationToken ct)
		=> Ok(await ChallengeService.UpdateQuestionAsync(id, questionId, dto, ct));

	[HttpDelete("{id:guid}/questions/{questionId:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteQuestion(Guid id, Guid questionId, CancellationToken ct)
	{
		await ChallengeService.DeleteQuestionAsync(id, questionId, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/questions/reorder")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ReorderQuestions(Guid id, [FromBody] ReorderQuestionsDto dto, CancellationToken ct)
	{
		await ChallengeService.ReorderQuestionsAsync(id, dto.OrderedIds, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/attempts")]
	[ProducesResponseType(typeof(PublicChallengeDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> StartAttempt(Guid id, CancellationToken ct)
		=> Created(string.Empty, await AttemptService.StartAsync(id, Me, ct));

	[HttpPost("attempts/{attemptId:guid}/submit")]
	[ProducesResponseType(typeof(ChallengeAttemptResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SubmitAttempt(Guid attemptId, [FromBody] SubmitChallengeAttemptDto dto, CancellationToken ct)
		=> Ok(await AttemptService.SubmitAsync(attemptId, dto, Me, ct));

	[HttpGet("me/attempts")]
	[ProducesResponseType(typeof(IReadOnlyList<ChallengeAttemptDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> MyAttempts(CancellationToken ct)
		=> Ok(await AttemptService.ListMyAttemptsAsync(Me, ct));
}
