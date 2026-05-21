using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;

namespace Waao.Services.Authorization;

/// <summary>
/// Centralized rank-based authorization for XP grants.
/// Per the WAAO economy rule, an actor can only grant XP to a recipient whose
/// role rank is STRICTLY LOWER than their own:
///   Admin (2) > HR (1) > Collaborator (0)
/// Admins cannot grant XP to other admins. Self-grants are also blocked.
/// </summary>
public static class RankGuard
{
	public static async Task EnsureCanGrantXpToAsync(WaaoDbContext db, Guid actorId, Guid targetId, CancellationToken ct = default)
	{
		if (actorId == targetId)
			throw new InvalidOperationException("You cannot grant XP to yourself.");

		var pair = await db.Collaborators
			.IgnoreQueryFilters()
			.Where(c => c.Id == actorId || c.Id == targetId)
			.Select(c => new { c.Id, c.RoleKind })
			.ToListAsync(ct);

		var actor = pair.FirstOrDefault(c => c.Id == actorId)
			?? throw new UnauthorizedAccessException("Actor not found.");
		var target = pair.FirstOrDefault(c => c.Id == targetId)
			?? throw new KeyNotFoundException($"Target {targetId} not found.");

		if ((int)actor.RoleKind <= (int)target.RoleKind)
			throw new InvalidOperationException(
				$"XP grants must be approved by a higher rank. Your rank ({actor.RoleKind}) is not above the recipient's ({target.RoleKind}).");
	}
}
