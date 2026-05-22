namespace Waao.Domain.Models.Entities.Calendar;

public class EventOccurrenceOverride : Entity
{
	public Guid EventId { get; set; }
	public virtual CalendarEvent Event { get; set; } = null!;

	/// <summary>Identifies WHICH occurrence of the series this overrides.</summary>
	public DateTime OriginalStartUtc { get; set; }

	/// <summary>True = this occurrence is removed from the series.</summary>
	public bool IsCancelled { get; set; }

	// Override fields — null = inherit from the base event.
	public string? Title { get; set; }
	public string? Description { get; set; }
	public string? Location { get; set; }
	public DateTime? StartsAtUtc { get; set; }
	public DateTime? EndsAtUtc { get; set; }
	public bool? IsAllDay { get; set; }
	public string? ColorHex { get; set; }
}
