using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Design;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao")]
[Authorize]
public class DesignFlowsController(IDesignFlowService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	// ----- Flows -----
	[HttpGet("design-flows")]
	[ProducesResponseType(typeof(IReadOnlyList<DesignFlowDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFlows(CancellationToken ct)
		=> Ok(await Service.GetFlowsAsync(ct));

	[HttpPost("design-flows")]
	[ProducesResponseType(typeof(DesignFlowDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateFlow([FromBody] CreateDesignFlowDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateFlowAsync(dto, ct));

	[HttpPut("design-flows/{id:guid}")]
	[ProducesResponseType(typeof(DesignFlowDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateFlow(Guid id, [FromBody] UpdateDesignFlowDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateFlowAsync(id, dto, ct));

	[HttpDelete("design-flows/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteFlow(Guid id, CancellationToken ct)
	{
		await Service.DeleteFlowAsync(id, ct);
		return NoContent();
	}

	// ----- Board -----
	[HttpGet("design-flows/{id:guid}/board")]
	[ProducesResponseType(typeof(DesignBoardDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetBoard(Guid id, CancellationToken ct)
		=> Ok(await Service.GetBoardAsync(id, ct));

	// ----- Steps -----
	[HttpPost("design-flows/{id:guid}/steps")]
	[ProducesResponseType(typeof(DesignStepDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateStep(Guid id, [FromBody] CreateDesignStepDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateStepAsync(id, dto, ct));

	[HttpPut("steps/{id:guid}")]
	[ProducesResponseType(typeof(DesignStepDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateStep(Guid id, [FromBody] UpdateDesignStepDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateStepAsync(id, dto, ct));

	[HttpDelete("steps/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteStep(Guid id, CancellationToken ct)
	{
		await Service.DeleteStepAsync(id, ct);
		return NoContent();
	}

	// ----- Edges -----
	[HttpPost("design-flows/{id:guid}/edges")]
	[ProducesResponseType(typeof(DesignEdgeDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> CreateEdge(Guid id, [FromBody] CreateDesignEdgeDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateEdgeAsync(id, dto, ct));

	[HttpDelete("edges/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteEdge(Guid id, CancellationToken ct)
	{
		await Service.DeleteEdgeAsync(id, ct);
		return NoContent();
	}

	// ----- Assets -----
	[HttpPost("steps/{id:guid}/assets")]
	[ProducesResponseType(typeof(DesignAssetDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
	[RequestSizeLimit(50_000_000)]
	public async Task<IActionResult> AddAsset(
		Guid id,
		[FromForm] IFormFile file,
		[FromServices] ILogger<DesignFlowsController> Logger,
		CancellationToken ct)
	{
		if (file is null || file.Length == 0)
			return BadRequest("Empty file.");
		if (file.Length > 50_000_000)
			return StatusCode(StatusCodes.Status413PayloadTooLarge);

		var mime = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
		try
		{
			await using var stream = file.OpenReadStream();
			var dto = await Service.AddAssetAsync(id, stream, file.FileName, mime, file.Length, Me, ct);
			return Created(string.Empty, dto);
		}
		catch (KeyNotFoundException)
		{
			throw;
		}
		catch (InvalidOperationException ex)
		{
			Logger.LogError(ex, "Design asset upload rejected step={Step} fileName={Name}", id, file.FileName);
			return StatusCode(StatusCodes.Status503ServiceUnavailable, ex.Message);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Design asset upload failed step={Step} fileName={Name} size={Size}", id, file.FileName, file.Length);
			return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
		}
	}

	[HttpPut("assets/{id:guid}")]
	[ProducesResponseType(typeof(DesignAssetDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateAsset(Guid id, [FromBody] UpdateDesignAssetDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateAssetAsync(id, dto, ct));

	[HttpDelete("assets/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteAsset(Guid id, CancellationToken ct)
	{
		await Service.DeleteAssetAsync(id, ct);
		return NoContent();
	}
}
