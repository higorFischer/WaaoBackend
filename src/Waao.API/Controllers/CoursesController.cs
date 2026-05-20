using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Courses;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/courses")]
[Authorize]
public class CoursesController(ICourseService CourseService, ICourseCompletionService CompletionService) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	private bool IsAdminOrHr => User.IsInRole("Admin") || User.IsInRole("HR");

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<CourseDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> List([FromQuery] CourseListFilterDto filter, CancellationToken ct)
		=> Ok(await CourseService.ListAsync(filter, IsAdminOrHr, ct));

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
		=> Ok(await CourseService.GetByIdAsync(id, IsAdminOrHr, ct));

	[HttpPost]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Create([FromBody] CreateCourseDto dto, CancellationToken ct)
		=> Created(string.Empty, await CourseService.CreateAsync(dto, Me, ct));

	[HttpPut("{id:guid}")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseDto dto, CancellationToken ct)
		=> Ok(await CourseService.UpdateAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
	{
		await CourseService.DeleteAsync(id, ct);
		return NoContent();
	}

	[HttpPost("{id:guid}/publish")]
	[Authorize(Policy = "HR")]
	[ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Publish(Guid id, [FromBody] PublishCourseDto dto, CancellationToken ct)
		=> Ok(await CourseService.PublishAsync(id, dto.IsPublished, ct));

	[HttpPost("{id:guid}/complete")]
	[ProducesResponseType(typeof(CourseCompletionDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> MarkComplete(Guid id, [FromBody] MarkCourseCompleteDto dto, CancellationToken ct)
		=> Ok(await CompletionService.MarkCompleteAsync(id, Me, dto, ct));

	[HttpGet("me/completions")]
	[ProducesResponseType(typeof(IReadOnlyList<CourseCompletionDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> MyCompletions(CancellationToken ct)
		=> Ok(await CompletionService.ListMyCompletionsAsync(Me, ct));
}
