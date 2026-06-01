using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Announcements;

public class Announcement : Entity
{
	public string Title { get; set; } = string.Empty;
	public string? Body { get; set; }
	public string? ImageUrl { get; set; }
	public string? LogoUrl { get; set; }

	public DateTime StartsAtUtc { get; set; }
	public DateTime EndsAtUtc { get; set; }

	public RecurrenceKind RecurrenceKind { get; set; } = RecurrenceKind.None;
	public DateTime? RecurrenceUntilUtc { get; set; }

	public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
	public Guid? DepartmentId { get; set; }
	public virtual Department? Department { get; set; }
	public CollaboratorRoleKind? TargetRoleKind { get; set; }

	public DateTime? CountdownToUtc { get; set; }
	public string? CountdownLabel { get; set; }

	public string AccentColorHex { get; set; } = "#FFB300";
	public AnnouncementEffect Effect { get; set; } = AnnouncementEffect.None;

	public int Position { get; set; }
	public bool IsArchived { get; set; }

	public Guid CreatedById { get; set; }
	public string CreatedByName { get; set; } = string.Empty;

	public virtual ICollection<AnnouncementTarget> Targets { get; set; } = [];
}

public class AnnouncementTarget : Entity
{
	public Guid AnnouncementId { get; set; }
	public virtual Announcement Announcement { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;
}
