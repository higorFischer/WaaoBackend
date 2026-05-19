using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Kanban;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/kanban")]
[Authorize]
public class KanbanController(IKanbanService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	// ----- BOARDS -----
	[HttpGet("boards")]
	public async Task<IActionResult> ListBoards(CancellationToken ct)
		=> Ok(await Service.ListBoardsAsync(Me, ct));

	[HttpGet("boards/{slug}")]
	public async Task<IActionResult> GetBoard(string slug, CancellationToken ct)
	{
		var b = await Service.GetBoardAsync(slug, Me, ct);
		return b is null ? NotFound() : Ok(b);
	}

	[HttpPost("boards")]
	public async Task<IActionResult> CreateBoard([FromBody] CreateBoardDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateBoardAsync(dto, Me, ct));

	[HttpPut("boards/{id:guid}")]
	public async Task<IActionResult> UpdateBoard(Guid id, [FromBody] UpdateBoardDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateBoardAsync(id, dto, Me, ct));

	[HttpDelete("boards/{id:guid}")]
	public async Task<IActionResult> DeleteBoard(Guid id, CancellationToken ct)
	{
		await Service.DeleteBoardAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("boards/{id:guid}/members")]
	public async Task<IActionResult> AddMember(Guid id, [FromBody] AddBoardMemberDto dto, CancellationToken ct)
	{
		await Service.AddMemberAsync(id, dto, Me, ct);
		return NoContent();
	}

	[HttpDelete("boards/{boardId:guid}/members/{memberId:guid}")]
	public async Task<IActionResult> RemoveMember(Guid boardId, Guid memberId, CancellationToken ct)
	{
		await Service.RemoveMemberAsync(boardId, memberId, Me, ct);
		return NoContent();
	}

	// ----- COLUMNS -----
	[HttpPost("boards/{boardId:guid}/columns")]
	public async Task<IActionResult> CreateColumn(Guid boardId, [FromBody] CreateColumnDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateColumnAsync(boardId, dto, Me, ct));

	[HttpPut("columns/{id:guid}")]
	public async Task<IActionResult> UpdateColumn(Guid id, [FromBody] UpdateColumnDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateColumnAsync(id, dto, Me, ct));

	[HttpDelete("columns/{id:guid}")]
	public async Task<IActionResult> DeleteColumn(Guid id, CancellationToken ct)
	{
		await Service.DeleteColumnAsync(id, Me, ct);
		return NoContent();
	}

	// ----- EPICS / LABELS -----
	[HttpPost("boards/{boardId:guid}/epics")]
	public async Task<IActionResult> CreateEpic(Guid boardId, [FromBody] CreateEpicDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateEpicAsync(boardId, dto, Me, ct));

	[HttpPost("boards/{boardId:guid}/labels")]
	public async Task<IActionResult> CreateLabel(Guid boardId, [FromBody] CreateLabelDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateLabelAsync(boardId, dto, Me, ct));

	// ----- CARDS -----
	[HttpPost("cards")]
	public async Task<IActionResult> CreateCard([FromBody] CreateCardDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateCardAsync(dto, Me, ct));

	[HttpGet("cards/{id:guid}")]
	public async Task<IActionResult> GetCard(Guid id, CancellationToken ct)
	{
		var card = await Service.GetCardAsync(id, Me, ct);
		return card is null ? NotFound() : Ok(card);
	}

	[HttpPut("cards/{id:guid}")]
	public async Task<IActionResult> UpdateCard(Guid id, [FromBody] UpdateCardDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateCardAsync(id, dto, Me, ct));

	[HttpPatch("cards/{id:guid}/move")]
	public async Task<IActionResult> MoveCard(Guid id, [FromBody] MoveCardDto dto, CancellationToken ct)
		=> Ok(await Service.MoveCardAsync(id, dto, Me, ct));

	[HttpDelete("cards/{id:guid}")]
	public async Task<IActionResult> ArchiveCard(Guid id, CancellationToken ct)
	{
		await Service.ArchiveCardAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("cards/{cardId:guid}/labels/{labelId:guid}")]
	public async Task<IActionResult> AddLabel(Guid cardId, Guid labelId, CancellationToken ct)
	{
		await Service.AddLabelToCardAsync(cardId, labelId, Me, ct);
		return NoContent();
	}

	[HttpDelete("cards/{cardId:guid}/labels/{labelId:guid}")]
	public async Task<IActionResult> RemoveLabel(Guid cardId, Guid labelId, CancellationToken ct)
	{
		await Service.RemoveLabelFromCardAsync(cardId, labelId, Me, ct);
		return NoContent();
	}

	// ----- COMMENTS / CHECKLISTS -----
	[HttpPost("cards/{cardId:guid}/comments")]
	public async Task<IActionResult> CreateComment(Guid cardId, [FromBody] CreateCommentDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateCommentAsync(cardId, dto, Me, ct));

	[HttpPut("comments/{id:guid}")]
	public async Task<IActionResult> UpdateComment(Guid id, [FromBody] UpdateCommentDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateCommentAsync(id, dto, Me, ct));

	[HttpDelete("comments/{id:guid}")]
	public async Task<IActionResult> DeleteComment(Guid id, CancellationToken ct)
	{
		await Service.DeleteCommentAsync(id, Me, ct);
		return NoContent();
	}

	[HttpPost("cards/{cardId:guid}/checklists")]
	public async Task<IActionResult> CreateChecklist(Guid cardId, [FromBody] CreateChecklistDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateChecklistAsync(cardId, dto, Me, ct));

	[HttpPost("checklists/{checklistId:guid}/items")]
	public async Task<IActionResult> CreateChecklistItem(Guid checklistId, [FromBody] CreateChecklistItemDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateChecklistItemAsync(checklistId, dto, Me, ct));

	[HttpPatch("checklist-items/{id:guid}")]
	public async Task<IActionResult> UpdateChecklistItem(Guid id, [FromBody] UpdateChecklistItemDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateChecklistItemAsync(id, dto, Me, ct));
}
