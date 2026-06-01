using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities;

public class Collaborator : Entity
{
	/// <summary>The tenant this collaborator belongs to. Phase 1 added the column nullable
	/// and backfilled to WAAO; later phases tighten to NOT NULL.</summary>
	public Guid? TenantId { get; set; }
	public virtual Tenant? Tenant { get; set; }

	public string FullName { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? Cpf { get; set; }
	public DateOnly? Birthdate { get; set; }
	public DateOnly JoinDate { get; set; }
	public DateOnly? TerminationDate { get; set; }
	public string? PhotoUrl { get; set; }
	public string? Bio { get; set; }
	public CollaboratorStatus Status { get; set; } = CollaboratorStatus.Active;

	public Guid? DepartmentId { get; set; }
	public virtual Department? Department { get; set; }

	public Guid? RoleId { get; set; }
	public virtual Role? Role { get; set; }

	public Guid? ManagerId { get; set; }
	public virtual Collaborator? Manager { get; set; }
	public virtual ICollection<Collaborator> DirectReports { get; set; } = [];

	// ----- Gamification state -----
	public long TotalXp { get; set; }
	public int CurrentLevel { get; set; } = 0;
	public int CurrentStreakDays { get; set; }
	public int LongestStreakDays { get; set; }
	public DateOnly? LastActivityDate { get; set; }

	public bool OptInLeaderboards { get; set; } = true;

	/// <summary>
	/// Whether the user wants OS-level desktop notifications. Stored
	/// server-side so signing in on a new browser/device can auto-prompt the
	/// permission instead of forcing the user to find the toggle in Settings.
	/// </summary>
	public bool DesktopNotificationsEnabled { get; set; }

	// Schema migration intentionally deferred to plan Task 7 (ManualXpEconomyReset).
	public DateTime? OnboardingCompletedAt { get; set; }

	// ----- Auth -----
	// Schema migration intentionally deferred to plan Task 2 (AddEmailVerification).
	public bool EmailVerified { get; set; }
	public string? EmailVerificationToken { get; set; }
	public DateTime? EmailVerificationTokenExpiresAt { get; set; }
	public DateTime? EmailVerifiedAt { get; set; }
	public DateTime? LastVerificationEmailSentAt { get; set; }

	public string? PasswordHash { get; set; }
	public CollaboratorRoleKind RoleKind { get; set; } = CollaboratorRoleKind.Collaborator;
	public DateTime? LastLoginAt { get; set; }
	public DateOnly? LastLoginDate { get; set; }
	public int CurrentLoginStreakDays { get; set; }
	public int LongestLoginStreakDays { get; set; }

	public virtual ICollection<CareerEvent> CareerEvents { get; set; } = [];
	public virtual ICollection<CollaboratorBadge> Badges { get; set; } = [];
	public virtual ICollection<XpTransaction> XpTransactions { get; set; } = [];
}
