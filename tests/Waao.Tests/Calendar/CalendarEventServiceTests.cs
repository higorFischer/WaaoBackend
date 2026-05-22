using FluentAssertions;
using Xunit;
using Waao.Domain.Models.Entities.Calendar;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Calendar;
using Waao.Services.Services;
using Waao.Tests.Support;
using CalendarEntity = Waao.Domain.Models.Entities.Calendar.Calendar;

namespace Waao.Tests.Calendar;

public class CalendarEventServiceTests
{
	private static (CalendarService calSvc, CalendarEventService evtSvc, Waao.Infra.EF.WaaoDbContext db)
		Build()
	{
		var db = TestDb.New();
		var expander = new RecurrenceExpander();
		var calSvc = new CalendarService(db);
		var evtSvc = new CalendarEventService(db, expander, calSvc);
		return (calSvc, evtSvc, db);
	}

	private static async Task<(Guid calId, Guid colId)> SeedPersonalCalendar(
		Waao.Infra.EF.WaaoDbContext db, CalendarService svc)
	{
		var colId = Guid.CreateVersion7();
		db.Collaborators.Add(new Waao.Domain.Models.Entities.Collaborator
		{
			Id = colId,
			FullName = "Test User",
			Email = "test@example.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
		});
		await db.SaveChangesAsync();
		var calId = await svc.EnsurePersonalCalendarAsync(colId);
		return (calId, colId);
	}

	// ---- GetOccurrencesAsync — basic weekly expansion ----

	[Fact]
	public async Task WeeklyEvent_GetOccurrences_OverOneMonth_ReturnsCorrectCount()
	{
		var (calSvc, evtSvc, db) = Build();
		var (calId, colId) = await SeedPersonalCalendar(db, calSvc);

		// Weekly Monday event starting 2026-06-01
		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		await evtSvc.CreateAsync(new CreateCalendarEventDto
		{
			CalendarId = calId,
			Title = "Weekly Meeting",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
		}, colId);

		var query = new EventWindowQueryDto
		{
			FromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
			ToUtc = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
		};
		var occurrences = await evtSvc.GetOccurrencesAsync(query, colId);

		// June 2026 Mondays: 1, 8, 15, 22, 29 = 5
		occurrences.Should().HaveCount(5);
		occurrences.Should().OnlyContain(o => o.IsRecurring);
	}

	// ---- editScope=this: writes override ----

	[Fact]
	public async Task UpdateAsync_ScopeThis_WritesOccurrenceOverride()
	{
		var (calSvc, evtSvc, db) = Build();
		var (calId, colId) = await SeedPersonalCalendar(db, calSvc);

		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var eventId = await CreateWeeklyEvent(evtSvc, calId, colId, start);

		// Edit only the first occurrence
		var originalStart = start;
		await evtSvc.UpdateAsync(eventId, new UpdateCalendarEventDto
		{
			Title = "Changed First",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(2),
		}, "this", originalStart, colId);

		var overrides = db.EventOccurrenceOverrides.ToList();
		overrides.Should().ContainSingle();
		overrides[0].Title.Should().Be("Changed First");
		overrides[0].OriginalStartUtc.Should().Be(originalStart);
		overrides[0].IsCancelled.Should().BeFalse();
	}

	// ---- editScope=this with delete: cancels occurrence ----

	[Fact]
	public async Task DeleteAsync_ScopeThis_CancelsOccurrence()
	{
		var (calSvc, evtSvc, db) = Build();
		var (calId, colId) = await SeedPersonalCalendar(db, calSvc);

		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var eventId = await CreateWeeklyEvent(evtSvc, calId, colId, start);

		await evtSvc.DeleteAsync(eventId, "this", start, colId);

		// Cancelled occurrence should be dropped from expansions
		var query = new EventWindowQueryDto
		{
			FromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
			ToUtc = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc),
		};
		var occurrences = await evtSvc.GetOccurrencesAsync(query, colId);
		occurrences.Should().HaveCount(4); // 5 - 1 cancelled
	}

	// ---- editScope=thisAndFuture: truncates base + creates new series ----

	[Fact]
	public async Task UpdateAsync_ScopeThisAndFuture_TruncatesAndCreatesNewSeries()
	{
		var (calSvc, evtSvc, db) = Build();
		var (calId, colId) = await SeedPersonalCalendar(db, calSvc);

		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var eventId = await CreateWeeklyEvent(evtSvc, calId, colId, start);

		// Split at June 15 (the 3rd Monday)
		var splitAt = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);
		await evtSvc.UpdateAsync(eventId, new UpdateCalendarEventDto
		{
			Title = "Updated From June 15",
			StartsAtUtc = splitAt,
			EndsAtUtc = splitAt.AddHours(1),
			RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
		}, "thisAndFuture", splitAt, colId);

		// Original series should now end before June 15
		var original = db.CalendarEvents.First(e => e.Id == eventId);
		original.RecurrenceEndUtc.Should().NotBeNull();
		original.RecurrenceEndUtc!.Value.Should().BeBefore(splitAt);

		// A new event should have been created
		var newEvents = db.CalendarEvents.Where(e => e.Id != eventId).ToList();
		newEvents.Should().ContainSingle();
		newEvents[0].Title.Should().Be("Updated From June 15");
	}

	// ---- editScope=all: updates the base event ----

	[Fact]
	public async Task UpdateAsync_ScopeAll_MutatesBaseEvent()
	{
		var (calSvc, evtSvc, db) = Build();
		var (calId, colId) = await SeedPersonalCalendar(db, calSvc);

		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var eventId = await CreateWeeklyEvent(evtSvc, calId, colId, start);

		await evtSvc.UpdateAsync(eventId, new UpdateCalendarEventDto
		{
			Title = "Renamed All",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
		}, "all", null, colId);

		var updated = db.CalendarEvents.First(e => e.Id == eventId);
		updated.Title.Should().Be("Renamed All");
	}

	// ---- Cross-scope write → 403 ----

	[Fact]
	public async Task CreateAsync_WrongScope_ThrowsUnauthorized()
	{
		var (calSvc, evtSvc, db) = Build();

		// Calendar owned by collaborator A
		var colA = Guid.CreateVersion7();
		db.Collaborators.Add(new Waao.Domain.Models.Entities.Collaborator
		{
			Id = colA, FullName = "A", Email = "a@test.com", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
		});
		await db.SaveChangesAsync();
		var calIdA = await calSvc.EnsurePersonalCalendarAsync(colA);

		// Collaborator B tries to write to A's personal calendar
		var colB = Guid.CreateVersion7();
		db.Collaborators.Add(new Waao.Domain.Models.Entities.Collaborator
		{
			Id = colB, FullName = "B", Email = "b@test.com", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
		});
		await db.SaveChangesAsync();

		var act = async () => await evtSvc.CreateAsync(new CreateCalendarEventDto
		{
			CalendarId = calIdA,
			Title = "Sneaky",
			StartsAtUtc = DateTime.UtcNow,
			EndsAtUtc = DateTime.UtcNow.AddHours(1),
		}, colB);

		await act.Should().ThrowAsync<UnauthorizedAccessException>();
	}

	private static async Task<Guid> CreateWeeklyEvent(
		CalendarEventService svc, Guid calId, Guid colId, DateTime start)
	{
		var dto = new CreateCalendarEventDto
		{
			CalendarId = calId,
			Title = "Weekly",
			StartsAtUtc = start,
			EndsAtUtc = start.AddHours(1),
			RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
		};
		var created = await svc.CreateAsync(dto, colId);
		return created.Id;
	}
}
