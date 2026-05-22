namespace Waao.Services.Abstractions.Services;

/// <summary>Expands a recurrence rule into concrete occurrence start times within a window.</summary>
public interface IRecurrenceExpander
{
	/// <summary>
	/// Returns UTC occurrence start times within [windowStartUtc, windowEndUtc].
	/// Null rule → single occurrence at startsAtUtc if it falls in the window; empty otherwise.
	/// Hard cap: 366 results per call.
	/// </summary>
	IReadOnlyList<DateTime> Expand(
		string? recurrenceRule,
		DateTime startsAtUtc,
		DateTime? recurrenceEndUtc,
		DateTime windowStartUtc,
		DateTime windowEndUtc);
}
