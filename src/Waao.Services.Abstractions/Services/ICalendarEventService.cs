using Waao.Services.Abstractions.Dtos.Calendar;

namespace Waao.Services.Abstractions.Services;

public interface ICalendarEventService
{
	/// <summary>Returns visible occurrences in the query window, with overrides applied.</summary>
	Task<IReadOnlyList<CalendarOccurrenceDto>> GetOccurrencesAsync(
		EventWindowQueryDto query, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>Returns the base event row for the edit form.</summary>
	Task<CalendarEventDto> GetEventAsync(Guid eventId, Guid collaboratorId, CancellationToken ct = default);

	Task<CalendarEventDto> CreateAsync(CreateCalendarEventDto dto, Guid collaboratorId, CancellationToken ct = default);

	/// <summary>
	/// Update with edit-scope semantics.
	/// editScope: "this" | "thisAndFuture" | "all".
	/// originalStartUtc required when editScope is "this" or "thisAndFuture".
	/// </summary>
	Task<CalendarEventDto> UpdateAsync(
		Guid eventId,
		UpdateCalendarEventDto dto,
		string editScope,
		DateTime? originalStartUtc,
		Guid collaboratorId,
		CancellationToken ct = default);

	/// <summary>Delete with edit-scope semantics.</summary>
	Task DeleteAsync(
		Guid eventId,
		string editScope,
		DateTime? originalStartUtc,
		Guid collaboratorId,
		CancellationToken ct = default);
}
