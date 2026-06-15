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
	/// <summary>
	/// Team Management data is ADMIN-ONLY. Only administrators may read or write anything
	/// recorded about a person (skills assessments, manager notes) or view team rollups.
	/// HR, a person's manager, and the person themselves have NO access.
	/// </summary>
	public static bool IsAdmin(Collaborator caller)
		=> caller.RoleKind == CollaboratorRoleKind.Admin;

	/// <summary>Caller may manage the target's team data only if they are an administrator.
	/// (<paramref name="target"/> retained for call-site stability / future per-target rules.)</summary>
	public static bool CanManage(Collaborator caller, Collaborator target)
		=> IsAdmin(caller);

	/// <summary>Private review material — administrators only.</summary>
	public static bool CanReadManagerNotes(Collaborator caller, Collaborator target)
		=> IsAdmin(caller);
}
