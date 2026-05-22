using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Calendar;

/// <summary>
/// Wraps Ical.Net to expand an RRULE into concrete occurrence start times
/// within a requested [windowStart, windowEnd] window.
/// Hard cap: 366 results per call to bound expansion cost.
/// </summary>
public sealed class RecurrenceExpander : IRecurrenceExpander
{
	private const int HardCap = 366;

	public IReadOnlyList<DateTime> Expand(
		string? recurrenceRule,
		DateTime startsAtUtc,
		DateTime? recurrenceEndUtc,
		DateTime windowStartUtc,
		DateTime windowEndUtc)
	{
		// Non-recurring: single occurrence if it falls within the window.
		if (string.IsNullOrWhiteSpace(recurrenceRule))
		{
			return startsAtUtc >= windowStartUtc && startsAtUtc <= windowEndUtc
				? [startsAtUtc]
				: [];
		}

		try
		{
			var pattern = new RecurrencePattern(recurrenceRule);

			// Apply series end if specified.
			if (recurrenceEndUtc.HasValue)
				pattern.Until = new CalDateTime(DateTime.SpecifyKind(recurrenceEndUtc.Value, DateTimeKind.Utc));

			var calEvent = new CalendarEvent
			{
				DtStart = new CalDateTime(DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc)),
				RecurrenceRule = pattern,
			};

			// GetOccurrences(CalDateTime periodStart, EvaluationOptions? options)
			var periodStart = new CalDateTime(DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc));
			var options = new EvaluationOptions();

			var result = new List<DateTime>(HardCap);
			foreach (var occ in calEvent.GetOccurrences(periodStart, options))
			{
				var occStart = DateTime.SpecifyKind(occ.Period.StartTime.Value, DateTimeKind.Utc);
				// Ical.Net returns occurrences from the series start; filter to our window end.
				if (occStart > windowEndUtc) break;
				if (result.Count >= HardCap) break;
				result.Add(occStart);
			}
			return result;
		}
		catch
		{
			// Invalid RRULE — treat as non-recurring.
			return startsAtUtc >= windowStartUtc && startsAtUtc <= windowEndUtc
				? [startsAtUtc]
				: [];
		}
	}
}
