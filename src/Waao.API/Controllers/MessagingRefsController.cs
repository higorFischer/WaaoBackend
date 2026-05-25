using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Messaging;

namespace Waao.API.Controllers;

/// <summary>
/// Lightweight lookups powering the chat composer's "reference a card / meeting"
/// pickers. Returns the smallest dataset needed to render a search row.
/// </summary>
[ApiController]
[Route("api/waao/messaging/refs")]
[Authorize]
public class MessagingRefsController(WaaoDbContext Db) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("cards")]
	[ProducesResponseType(typeof(IReadOnlyList<CardRefDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> SearchCards([FromQuery] string? q, [FromQuery] int limit = 20, CancellationToken ct = default)
	{
		var callerId = Me;
		var caller = await Db.Collaborators
			.Include(c => c.Role)
			.FirstOrDefaultAsync(c => c.Id == callerId, ct);
		var isAdmin = caller?.RoleKind == CollaboratorRoleKind.Admin;
		var seniorityOrder = caller?.Role?.SeniorityOrder;

		var query = Db.Cards
			.Include(c => c.Board).ThenInclude(b => b.Members)
			.Where(c => !c.IsArchived && !c.Board.IsArchived);

		query = query.Where(c =>
			isAdmin ||
			c.Board.Visibility == BoardVisibility.Public ||
			c.Board.OwnerId == callerId ||
			c.Board.Members.Any(m => m.CollaboratorId == callerId) ||
			(c.Board.MinSeniorityOrder != null && seniorityOrder != null && seniorityOrder >= c.Board.MinSeniorityOrder));

		if (!string.IsNullOrWhiteSpace(q))
		{
			var qLower = q.Trim().ToLower();
			query = query.Where(c => c.Title.ToLower().Contains(qLower));
		}

		var capped = Math.Clamp(limit, 1, 50);
		var rows = await query
			.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
			.Take(capped)
			.Select(c => new CardRefDto
			{
				Id = c.Id,
				Title = c.Title,
				BoardSlug = c.Board.Slug,
				BoardTitle = c.Board.Title,
			})
			.ToListAsync(ct);

		return Ok(rows);
	}

	[HttpGet("meetings")]
	[ProducesResponseType(typeof(IReadOnlyList<MeetingRefDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> SearchMeetings([FromQuery] string? q, [FromQuery] int limit = 20, CancellationToken ct = default)
	{
		var callerId = Me;

		var attendedIds = await Db.MeetingAttendees
			.Where(a => a.CollaboratorId == callerId)
			.Select(a => a.MeetingId)
			.ToListAsync(ct);

		var query = Db.Meetings
			.Include(m => m.CalendarEvent)
			.Where(m => m.OrganizerId == callerId || attendedIds.Contains(m.Id));

		if (!string.IsNullOrWhiteSpace(q))
		{
			var qLower = q.Trim().ToLower();
			query = query.Where(m => m.CalendarEvent.Title.ToLower().Contains(qLower));
		}

		var capped = Math.Clamp(limit, 1, 50);
		var rows = await query
			.OrderByDescending(m => m.CalendarEvent.StartsAtUtc)
			.Take(capped)
			.Select(m => new MeetingRefDto
			{
				Id = m.Id,
				Title = m.CalendarEvent.Title,
				StartsAtUtc = m.CalendarEvent.StartsAtUtc,
			})
			.ToListAsync(ct);

		return Ok(rows);
	}
}
