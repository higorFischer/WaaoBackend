using FluentAssertions;
using Xunit;
using Waao.Services.Calendar;
using Waao.Services.Abstractions.Services;

namespace Waao.Tests.Calendar;

public class RecurrenceExpanderTests
{
	private readonly IRecurrenceExpander _expander = new RecurrenceExpander();

	// ---- Null rule → single occurrence ----

	[Fact]
	public void NullRule_ReturnsStartsAt_WhenInWindow()
	{
		var start = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
		var result = _expander.Expand(null, start, null, start.AddHours(-1), start.AddHours(2));
		result.Should().ContainSingle().Which.Should().Be(start);
	}

	[Fact]
	public void NullRule_ReturnsEmpty_WhenOutsideWindow()
	{
		var start = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
		var result = _expander.Expand(null, start, null, start.AddDays(1), start.AddDays(2));
		result.Should().BeEmpty();
	}

	// ---- Weekly expansion ----

	[Fact]
	public void WeeklyRule_OverOneMonth_ReturnsCorrectCount()
	{
		// Every Monday starting 2026-06-01 (a Monday)
		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var windowStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

		var result = _expander.Expand("FREQ=WEEKLY;BYDAY=MO", start, null, windowStart, windowEnd);

		// June 2026: Mondays on 1, 8, 15, 22, 29 = 5
		result.Should().HaveCount(5);
		result.Should().OnlyContain(dt => dt.DayOfWeek == DayOfWeek.Monday);
	}

	// ---- Daily expansion ----

	[Fact]
	public void DailyRule_ExpandsCorrectly()
	{
		var start = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
		var windowStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2026, 6, 7, 23, 59, 59, DateTimeKind.Utc);

		var result = _expander.Expand("FREQ=DAILY", start, null, windowStart, windowEnd);

		result.Should().HaveCount(7);
	}

	// ---- Monthly expansion ----

	[Fact]
	public void MonthlyRule_ExpandsCorrectly()
	{
		var start = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
		var windowStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

		var result = _expander.Expand("FREQ=MONTHLY", start, null, windowStart, windowEnd);

		result.Should().HaveCount(12);
		result.Should().OnlyContain(dt => dt.Day == 15);
	}

	// ---- Window boundary: occurrence exactly on from/to ----

	[Fact]
	public void WindowBoundary_IncludesOccurrenceExactlyOnFrom()
	{
		var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		var result = _expander.Expand(null, start, null, start, start.AddDays(1));
		result.Should().ContainSingle().Which.Should().Be(start);
	}

	[Fact]
	public void WindowBoundary_IncludesOccurrenceExactlyOnTo()
	{
		var start = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);
		var result = _expander.Expand(null, start, null, start.AddDays(-1), windowEnd);
		result.Should().ContainSingle().Which.Should().Be(start);
	}

	// ---- RecurrenceEndUtc honored ----

	[Fact]
	public void RecurrenceEndUtc_HonorsSeriesStopDate()
	{
		var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var seriesEnd = new DateTime(2026, 6, 14, 23, 59, 59, DateTimeKind.Utc);
		var windowStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

		// Daily, but series ends June 14 — should get 14 occurrences
		var result = _expander.Expand("FREQ=DAILY", start, seriesEnd, windowStart, windowEnd);

		result.Should().HaveCount(14);
		result.Last().Should().BeBefore(seriesEnd.AddSeconds(1));
	}

	// ---- 366-occurrence hard cap ----

	[Fact]
	public void HardCap_Returns366WhenRuleWouldExceedIt()
	{
		var start = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var windowEnd = new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);

		var result = _expander.Expand("FREQ=DAILY", start, null, start, windowEnd);

		result.Should().HaveCount(366);
	}
}
