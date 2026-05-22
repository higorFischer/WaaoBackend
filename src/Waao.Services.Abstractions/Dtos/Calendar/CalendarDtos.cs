using Waao.Domain.Models.Enums;

namespace Waao.Services.Abstractions.Dtos.Calendar;

public record CalendarDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string ColorHex { get; init; } = string.Empty;
	public CalendarScope Scope { get; init; }
	public Guid? OwnerId { get; init; }
	public Guid? DepartmentId { get; init; }
	public string? DepartmentName { get; init; }
	public bool CanEdit { get; init; }
}

public record CalendarEventDto
{
	public Guid Id { get; init; }
	public Guid CalendarId { get; init; }
	public Guid CreatedById { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? ColorHex { get; init; }
	public string? RecurrenceRule { get; init; }
	public DateTime? RecurrenceEndUtc { get; init; }
}

/// <summary>One expanded occurrence for rendering in calendar views.</summary>
public record CalendarOccurrenceDto
{
	public Guid EventId { get; init; }
	public DateTime OccurrenceStartUtc { get; init; }
	public DateTime OriginalStartUtc { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? EffectiveColorHex { get; init; }
	public Guid CalendarId { get; init; }
	public bool IsRecurring { get; init; }
	public bool IsOverride { get; init; }
}

public record CreateCalendarEventDto
{
	public Guid CalendarId { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? ColorHex { get; init; }
	public string? RecurrenceRule { get; init; }
	public DateTime? RecurrenceEndUtc { get; init; }
}

public record UpdateCalendarEventDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? Location { get; init; }
	public DateTime StartsAtUtc { get; init; }
	public DateTime EndsAtUtc { get; init; }
	public bool IsAllDay { get; init; }
	public string? ColorHex { get; init; }
	public string? RecurrenceRule { get; init; }
	public DateTime? RecurrenceEndUtc { get; init; }
}

public record EventWindowQueryDto
{
	public DateTime FromUtc { get; init; }
	public DateTime ToUtc { get; init; }
	public Guid[]? CalendarIds { get; init; }
}
