namespace Waao.Domain.Models.Entities.Calendar;

public class CalendarEvent : Entity
{
	public Guid CalendarId { get; set; }
	public virtual Calendar Calendar { get; set; } = null!;

	public Guid CreatedById { get; set; }
	public virtual Collaborator CreatedBy { get; set; } = null!;

	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Location { get; set; }

	public DateTime StartsAtUtc { get; set; }
	public DateTime EndsAtUtc { get; set; }
	public bool IsAllDay { get; set; }

	/// <summary>Overrides the calendar's color when set.</summary>
	public string? ColorHex { get; set; }

	/// <summary>RRULE string (e.g. FREQ=WEEKLY;BYDAY=MO). Null = single event.</summary>
	public string? RecurrenceRule { get; set; }

	/// <summary>Series stop date. Null + a rule = open-ended (capped at expansion time).</summary>
	public DateTime? RecurrenceEndUtc { get; set; }

	public virtual ICollection<EventOccurrenceOverride> Overrides { get; set; } = [];
}
