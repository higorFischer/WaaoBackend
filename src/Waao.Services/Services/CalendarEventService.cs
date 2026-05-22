using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class CalendarEventService(
	WaaoDbContext Db,
	IRecurrenceExpander Expander,
	ICalendarService CalendarService) : ICalendarEventService
{
	// =====================================================================
	// QUERY
	// =====================================================================

	public async Task<IReadOnlyList<CalendarOccurrenceDto>> GetOccurrencesAsync(
		EventWindowQueryDto query,
		Guid collaboratorId,
		CancellationToken ct = default)
	{
		// Get visible calendar IDs for this collaborator.
		var visibleCalendars = await CalendarService.ListVisibleCalendarsAsync(collaboratorId, ct);
		var visibleIds = visibleCalendars.Select(c => c.Id).ToHashSet();

		// Optionally filter by requested calendar IDs.
		if (query.CalendarIds is { Length: > 0 })
			visibleIds.IntersectWith(query.CalendarIds);

		// Fetch base events that could have occurrences in the window:
		// 1. Non-recurring events whose starts_at falls in [from, to]
		// 2. Recurring events (recurrence_rule IS NOT NULL) — we need all of them to expand
		var baseEvents = await Db.CalendarEvents
			.Where(e =>
				visibleIds.Contains(e.CalendarId) &&
				(
					(e.RecurrenceRule == null && e.StartsAtUtc >= query.FromUtc && e.StartsAtUtc <= query.ToUtc) ||
					(e.RecurrenceRule != null && (e.RecurrenceEndUtc == null || e.RecurrenceEndUtc >= query.FromUtc))
				))
			.ToListAsync(ct);

		if (baseEvents.Count == 0) return [];

		var eventIds = baseEvents.Select(e => e.Id).ToList();

		// Load all overrides for these events in the window.
		var overrides = await Db.EventOccurrenceOverrides
			.Where(o => eventIds.Contains(o.EventId) && o.OriginalStartUtc >= query.FromUtc && o.OriginalStartUtc <= query.ToUtc)
			.ToListAsync(ct);

		var overrideByKey = overrides
			.GroupBy(o => (o.EventId, o.OriginalStartUtc))
			.ToDictionary(g => g.Key, g => g.First());

		var result = new List<CalendarOccurrenceDto>();

		foreach (var evt in baseEvents)
		{
			var occurrenceStarts = Expander.Expand(
				evt.RecurrenceRule,
				evt.StartsAtUtc,
				evt.RecurrenceEndUtc,
				query.FromUtc,
				query.ToUtc);

			foreach (var occStart in occurrenceStarts)
			{
				var key = (evt.Id, occStart);
				overrideByKey.TryGetValue(key, out var over);

				// Skip cancelled occurrences.
				if (over?.IsCancelled == true) continue;

				var duration = evt.EndsAtUtc - evt.StartsAtUtc;
				var effectiveStart = over?.StartsAtUtc ?? occStart;
				var effectiveEnd = over?.EndsAtUtc ?? (occStart + duration);

				result.Add(new CalendarOccurrenceDto
				{
					EventId = evt.Id,
					OccurrenceStartUtc = effectiveStart,
					OriginalStartUtc = occStart,
					Title = over?.Title ?? evt.Title,
					Description = over?.Description ?? evt.Description,
					Location = over?.Location ?? evt.Location,
					StartsAtUtc = effectiveStart,
					EndsAtUtc = effectiveEnd,
					IsAllDay = over?.IsAllDay ?? evt.IsAllDay,
					EffectiveColorHex = over?.ColorHex ?? evt.ColorHex,
					CalendarId = evt.CalendarId,
					IsRecurring = evt.RecurrenceRule is not null,
					IsOverride = over is not null,
				});
			}
		}

		return result.OrderBy(o => o.StartsAtUtc).ToList();
	}

	public async Task<CalendarEventDto> GetEventAsync(Guid eventId, Guid collaboratorId, CancellationToken ct = default)
	{
		var evt = await Db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
			?? throw new KeyNotFoundException($"CalendarEvent {eventId} not found.");

		await EnsureCanReadCalendarAsync(evt.CalendarId, collaboratorId, ct);
		return MapToDto(evt);
	}

	// =====================================================================
	// MUTATIONS
	// =====================================================================

	public async Task<CalendarEventDto> CreateAsync(
		CreateCalendarEventDto dto,
		Guid collaboratorId,
		CancellationToken ct = default)
	{
		await EnsureCanWriteCalendarAsync(dto.CalendarId, collaboratorId, ct);

		var evt = new CalendarEvent
		{
			Id = Guid.CreateVersion7(),
			CalendarId = dto.CalendarId,
			CreatedById = collaboratorId,
			Title = dto.Title,
			Description = dto.Description,
			Location = dto.Location,
			StartsAtUtc = dto.StartsAtUtc,
			EndsAtUtc = dto.EndsAtUtc,
			IsAllDay = dto.IsAllDay,
			ColorHex = dto.ColorHex,
			RecurrenceRule = dto.RecurrenceRule,
			RecurrenceEndUtc = dto.RecurrenceEndUtc,
			CreatedAt = DateTime.UtcNow,
		};
		Db.CalendarEvents.Add(evt);
		await Db.SaveChangesAsync(ct);
		return MapToDto(evt);
	}

	public async Task<CalendarEventDto> UpdateAsync(
		Guid eventId,
		UpdateCalendarEventDto dto,
		string editScope,
		DateTime? originalStartUtc,
		Guid collaboratorId,
		CancellationToken ct = default)
	{
		var evt = await Db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
			?? throw new KeyNotFoundException($"CalendarEvent {eventId} not found.");

		await EnsureCanWriteCalendarAsync(evt.CalendarId, collaboratorId, ct);

		var isRecurring = evt.RecurrenceRule is not null;
		var scope = isRecurring ? editScope : "all";

		if (scope is "this" or "thisAndFuture" && originalStartUtc is null)
			throw new ArgumentException("originalStartUtc is required for editScope 'this' or 'thisAndFuture'.");

		switch (scope)
		{
			case "this":
				await UpsertOverrideAsync(evt, originalStartUtc!.Value, dto, false, ct);
				break;

			case "thisAndFuture":
				await SplitSeriesAsync(evt, originalStartUtc!.Value, dto, collaboratorId, ct);
				break;

			default: // "all"
				ApplyUpdateToEvent(evt, dto);
				await Db.SaveChangesAsync(ct);
				break;
		}

		var updated = await Db.CalendarEvents.FirstAsync(e => e.Id == eventId, ct);
		return MapToDto(updated);
	}

	public async Task DeleteAsync(
		Guid eventId,
		string editScope,
		DateTime? originalStartUtc,
		Guid collaboratorId,
		CancellationToken ct = default)
	{
		var evt = await Db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
			?? throw new KeyNotFoundException($"CalendarEvent {eventId} not found.");

		await EnsureCanWriteCalendarAsync(evt.CalendarId, collaboratorId, ct);

		var isRecurring = evt.RecurrenceRule is not null;
		var scope = isRecurring ? editScope : "all";

		if (scope is "this" or "thisAndFuture" && originalStartUtc is null)
			throw new ArgumentException("originalStartUtc is required for editScope 'this' or 'thisAndFuture'.");

		switch (scope)
		{
			case "this":
				await UpsertOverrideAsync(evt, originalStartUtc!.Value, null, cancelled: true, ct);
				break;

			case "thisAndFuture":
				// Truncate series just before the target occurrence.
				evt.RecurrenceEndUtc = originalStartUtc!.Value.AddSeconds(-1);
				evt.UpdatedAt = DateTime.UtcNow;
				// Soft-delete all future overrides.
				var futureOverrides = await Db.EventOccurrenceOverrides
					.Where(o => o.EventId == eventId && o.OriginalStartUtc >= originalStartUtc)
					.ToListAsync(ct);
				foreach (var o in futureOverrides) { o.IsDeleted = true; o.DeletedAt = DateTime.UtcNow; }
				await Db.SaveChangesAsync(ct);
				break;

			default: // "all"
				// Soft-delete the base event and all its overrides.
				evt.IsDeleted = true;
				evt.DeletedAt = DateTime.UtcNow;
				var allOverrides = await Db.EventOccurrenceOverrides
					.Where(o => o.EventId == eventId)
					.ToListAsync(ct);
				foreach (var o in allOverrides) { o.IsDeleted = true; o.DeletedAt = DateTime.UtcNow; }
				await Db.SaveChangesAsync(ct);
				break;
		}
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task EnsureCanReadCalendarAsync(Guid calendarId, Guid collaboratorId, CancellationToken ct)
	{
		var visible = await CalendarService.ListVisibleCalendarsAsync(collaboratorId, ct);
		if (!visible.Any(c => c.Id == calendarId))
			throw new UnauthorizedAccessException("Caller cannot access this calendar.");
	}

	private async Task EnsureCanWriteCalendarAsync(Guid calendarId, Guid collaboratorId, CancellationToken ct)
	{
		var visible = await CalendarService.ListVisibleCalendarsAsync(collaboratorId, ct);
		var cal = visible.FirstOrDefault(c => c.Id == calendarId)
			?? throw new UnauthorizedAccessException("Caller cannot write to this calendar.");
		if (!cal.CanEdit)
			throw new UnauthorizedAccessException("Caller does not have write access to this calendar.");
	}

	private async Task UpsertOverrideAsync(
		CalendarEvent evt,
		DateTime originalStartUtc,
		UpdateCalendarEventDto? dto,
		bool cancelled,
		CancellationToken ct)
	{
		var over = await Db.EventOccurrenceOverrides
			.FirstOrDefaultAsync(o => o.EventId == evt.Id && o.OriginalStartUtc == originalStartUtc, ct);

		if (over is null)
		{
			over = new EventOccurrenceOverride
			{
				Id = Guid.CreateVersion7(),
				EventId = evt.Id,
				OriginalStartUtc = originalStartUtc,
				CreatedAt = DateTime.UtcNow,
			};
			Db.EventOccurrenceOverrides.Add(over);
		}

		over.IsCancelled = cancelled;
		over.UpdatedAt = DateTime.UtcNow;

		if (!cancelled && dto is not null)
		{
			over.Title = dto.Title;
			over.Description = dto.Description;
			over.Location = dto.Location;
			over.StartsAtUtc = dto.StartsAtUtc;
			over.EndsAtUtc = dto.EndsAtUtc;
			over.IsAllDay = dto.IsAllDay;
			over.ColorHex = dto.ColorHex;
		}

		await Db.SaveChangesAsync(ct);
	}

	private async Task SplitSeriesAsync(
		CalendarEvent evt,
		DateTime splitAt,
		UpdateCalendarEventDto dto,
		Guid collaboratorId,
		CancellationToken ct)
	{
		// Truncate the original series just before the split point.
		evt.RecurrenceEndUtc = splitAt.AddSeconds(-1);
		evt.UpdatedAt = DateTime.UtcNow;

		// Migrate any future overrides to the new event (we'll create the new event first, update foreign keys after).
		var futureOverrides = await Db.EventOccurrenceOverrides
			.Where(o => o.EventId == evt.Id && o.OriginalStartUtc >= splitAt)
			.ToListAsync(ct);

		// Create the new event starting from the split point.
		var newEvt = new CalendarEvent
		{
			Id = Guid.CreateVersion7(),
			CalendarId = evt.CalendarId,
			CreatedById = collaboratorId,
			Title = dto.Title,
			Description = dto.Description,
			Location = dto.Location,
			StartsAtUtc = dto.StartsAtUtc,
			EndsAtUtc = dto.EndsAtUtc,
			IsAllDay = dto.IsAllDay,
			ColorHex = dto.ColorHex,
			RecurrenceRule = dto.RecurrenceRule,
			RecurrenceEndUtc = dto.RecurrenceEndUtc,
			CreatedAt = DateTime.UtcNow,
		};
		Db.CalendarEvents.Add(newEvt);

		// Move future overrides to the new event.
		foreach (var o in futureOverrides)
		{
			o.EventId = newEvt.Id;
			o.UpdatedAt = DateTime.UtcNow;
		}

		await Db.SaveChangesAsync(ct);
	}

	private static void ApplyUpdateToEvent(CalendarEvent evt, UpdateCalendarEventDto dto)
	{
		evt.Title = dto.Title;
		evt.Description = dto.Description;
		evt.Location = dto.Location;
		evt.StartsAtUtc = dto.StartsAtUtc;
		evt.EndsAtUtc = dto.EndsAtUtc;
		evt.IsAllDay = dto.IsAllDay;
		evt.ColorHex = dto.ColorHex;
		evt.RecurrenceRule = dto.RecurrenceRule;
		evt.RecurrenceEndUtc = dto.RecurrenceEndUtc;
		evt.UpdatedAt = DateTime.UtcNow;
	}

	private static CalendarEventDto MapToDto(CalendarEvent evt) => new()
	{
		Id = evt.Id,
		CalendarId = evt.CalendarId,
		CreatedById = evt.CreatedById,
		Title = evt.Title,
		Description = evt.Description,
		Location = evt.Location,
		StartsAtUtc = evt.StartsAtUtc,
		EndsAtUtc = evt.EndsAtUtc,
		IsAllDay = evt.IsAllDay,
		ColorHex = evt.ColorHex,
		RecurrenceRule = evt.RecurrenceRule,
		RecurrenceEndUtc = evt.RecurrenceEndUtc,
	};
}
