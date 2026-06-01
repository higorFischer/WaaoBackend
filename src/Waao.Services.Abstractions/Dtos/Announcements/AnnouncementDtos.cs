using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Announcements;

public record AnnouncementDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Body { get; init; }
	public string? ImageUrl { get; init; }
	public string? LogoUrl { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public RecurrenceKind RecurrenceKind { get; init; }
	public DateTime? RecurrenceUntilUtc { get; init; }
	public AnnouncementAudience Audience { get; init; }
	public Guid? DepartmentId { get; init; }
	public string? DepartmentName { get; init; }
	public CollaboratorRoleKind? TargetRoleKind { get; init; }
	public DateTime? CountdownToUtc { get; init; }
	public string? CountdownLabel { get; init; }
	public string AccentColorHex { get; init; } = "#FFB300";
	public AnnouncementEffect Effect { get; init; }
	public int Position { get; init; }
	public bool IsArchived { get; init; }
	public Guid CreatedById { get; init; }
	public string CreatedByName { get; init; } = string.Empty;
	public IReadOnlyList<Guid> TargetCollaboratorIds { get; init; } = [];
}

public record CreateAnnouncementDto
{
	public string Title { get; init; } = string.Empty;
	public string? Body { get; init; }
	public string? ImageUrl { get; init; }
	public string? LogoUrl { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public RecurrenceKind RecurrenceKind { get; init; } = RecurrenceKind.None;
	public DateTime? RecurrenceUntilUtc { get; init; }
	public AnnouncementAudience Audience { get; init; } = AnnouncementAudience.Everyone;
	public Guid? DepartmentId { get; init; }
	public CollaboratorRoleKind? TargetRoleKind { get; init; }
	public IReadOnlyList<Guid> TargetCollaboratorIds { get; init; } = [];
	public DateTime? CountdownToUtc { get; init; }
	public string? CountdownLabel { get; init; }
	public string? AccentColorHex { get; init; }
	public AnnouncementEffect Effect { get; init; } = AnnouncementEffect.None;
}

public record UpdateAnnouncementDto : CreateAnnouncementDto
{
	public int Position { get; init; }
}
