using Waao.Services.Abstractions.Dtos.Calendar;

namespace Waao.Services.Abstractions.Services;

public interface ICalendarService
{
	/// <summary>Lazily creates a collaborator's personal calendar on first use. Returns the calendar ID.</summary>
	Task<Guid> EnsurePersonalCalendarAsync(Guid collaboratorId, CancellationToken ct = default);

	/// <summary>
	/// Returns visible calendars for the caller: personal + their department's + company.
	/// CanEdit is always true under the "anyone in scope" rule.
	/// </summary>
	Task<IReadOnlyList<CalendarDto>> ListVisibleCalendarsAsync(Guid collaboratorId, CancellationToken ct = default);
}
