using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Documentation;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/documentation")]
[Authorize]
public class DocumentationController(IDocumentationService Service) : ControllerBase
{
	[HttpGet("tree")]
	[ProducesResponseType(typeof(DocTreeNodeDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetTree(CancellationToken ct)
		=> Ok(await Service.GetTreeAsync(ct));

	[HttpGet("file")]
	[ProducesResponseType(typeof(DocFileDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetFile([FromQuery] string path, CancellationToken ct)
	{
		var file = await Service.GetFileAsync(path, ct);
		return file is null ? NotFound() : Ok(file);
	}

	[HttpGet("search")]
	[ProducesResponseType(typeof(IReadOnlyList<DocSearchHitDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int max = 50, CancellationToken ct = default)
		=> Ok(await Service.SearchAsync(q, max, ct));

	[HttpPost("refresh")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(DocRefreshResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Refresh(CancellationToken ct)
		=> Ok(await Service.RefreshAsync(ct));
}
