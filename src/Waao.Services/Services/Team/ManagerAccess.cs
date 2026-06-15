using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;

namespace Waao.Services.Services.Team;

/// <summary>
/// Central authorization rules for the Team Management feature. Pure functions over two
/// <see cref="Collaborator"/> snapshots so they're trivially unit-testable and reused by
/// every skills/notes/my-team endpoint.
/// </summary>
public static class ManagerAccess
{
	/// <summary>HR and Admin are "staff" — they can manage anyone.</summary>
	public static bool IsStaff(Collaborator caller)
		=> caller.RoleKind is CollaboratorRoleKind.HR or CollaboratorRoleKind.Admin;

	/// <summary>Caller may manage the target if they are staff or the target's direct manager.</summary>
	public static bool CanManage(Collaborator caller, Collaborator target)
		=> IsStaff(caller) || target.ManagerId == caller.Id;

	/// <summary>
	/// Private review material: staff or the managing manager, but NEVER the subject —
	/// a collaborator can never read their own manager notes.
	/// </summary>
	public static bool CanReadManagerNotes(Collaborator caller, Collaborator target)
		=> caller.Id != target.Id && CanManage(caller, target);
}
