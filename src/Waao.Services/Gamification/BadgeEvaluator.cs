using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Kanban;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;

namespace Waao.Services.Gamification;

public sealed record EvalContext(
	Collaborator Collaborator,
	IReadOnlyList<CareerEvent> Events,
	int KanbanCardsCompleted,
	int KanbanCardsCompletedLast7Days,
	int KanbanCommentsAuthored,
	int KanbanEpicsCompleted);

public sealed class BadgeEvaluator(WaaoDbContext Db)
{
	public async Task<IReadOnlyList<Badge>> EvaluateAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var onboarded = await Db.Collaborators
			.Where(c => c.Id == collaboratorId)
			.Select(c => c.OnboardingCompletedAt)
			.FirstOrDefaultAsync(ct);
		if (onboarded is null)
			return [];

		var collaborator = await Db.Collaborators
			.Include(c => c.Badges).ThenInclude(b => b.Badge)
			.Include(c => c.DirectReports)
			.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct);
		if (collaborator is null) return [];

		var events = await Db.CareerEvents
			.Where(e => e.CollaboratorId == collaboratorId)
			.ToListAsync(ct);

		// Kanban context — completion is "I'm assignee or reporter and CompletedAt is set".
		var weekAgo = DateTime.UtcNow.AddDays(-7);
		var kanbanCardsCompleted = await Db.Cards.CountAsync(c =>
			c.CompletedAt.HasValue && (c.AssigneeId == collaboratorId || c.ReporterId == collaboratorId), ct);
		var kanbanCardsCompletedLast7 = await Db.Cards.CountAsync(c =>
			c.CompletedAt.HasValue && c.CompletedAt >= weekAgo
			&& (c.AssigneeId == collaboratorId || c.ReporterId == collaboratorId), ct);
		var kanbanComments = await Db.CardComments.CountAsync(c => c.AuthorId == collaboratorId, ct);
		// "Epic completed by me" = at least one card I owned completed the epic. Approx: any completed card on a fully-done epic that involved me.
		var kanbanEpicsCompleted = await Db.Cards
			.Where(c => c.EpicId.HasValue
				&& (c.AssigneeId == collaboratorId || c.ReporterId == collaboratorId)
				&& c.CompletedAt.HasValue)
			.Where(c => !Db.Cards.Any(x => x.EpicId == c.EpicId && !x.IsArchived && !x.CompletedAt.HasValue))
			.Select(c => c.EpicId!.Value)
			.Distinct()
			.CountAsync(ct);

		var ctx = new EvalContext(collaborator, events, kanbanCardsCompleted, kanbanCardsCompletedLast7, kanbanComments, kanbanEpicsCompleted);

		var allBadges = await Db.Badges.ToDictionaryAsync(b => b.Code, ct);
		var already = collaborator.Badges.Select(b => b.Badge.Code).ToHashSet();
		var awards = new List<Badge>();

		foreach (var (code, rule) in Rules)
		{
			if (already.Contains(code)) continue;
			if (!allBadges.TryGetValue(code, out var badge)) continue;
			if (!rule(ctx)) continue;

			Db.CollaboratorBadges.Add(new CollaboratorBadge
			{
				Id = Guid.CreateVersion7(),
				CollaboratorId = collaboratorId,
				BadgeId = badge.Id,
				EarnedAt = DateTime.UtcNow,
				Context = badge.UnlockRule,
			});

			awards.Add(badge);
		}
		return awards;
	}

	// ----- Rule registry ---------------------------------------------------

	private static readonly (string Code, Func<EvalContext, bool> Rule)[] Rules =
	[
		("WELCOME",          _ => true),
		("TENURE_90_DAYS",   x => TenureDays(x.Collaborator) >= 90),
		("TENURE_1_YEAR",    x => TenureDays(x.Collaborator) >= 365),
		("TENURE_3_YEARS",   x => TenureDays(x.Collaborator) >= 365 * 3),
		("TENURE_5_YEARS",   x => TenureDays(x.Collaborator) >= 365 * 5),
		("TENURE_10_YEARS",  x => TenureDays(x.Collaborator) >= 365 * 10),

		("FIRST_PROMOTION",  x => x.Events.Any(e => e.Type == CareerEventType.Promotion)),
		("TRIPLE_PROMOTED",  x => x.Events.Count(e => e.Type == CareerEventType.Promotion) >= 3),
		("LATERAL_MOVE",     x => x.Events.Any(e => e.Type == CareerEventType.Lateral)),
		("PERF_REVIEW",      x => x.Events.Any(e => e.Type == CareerEventType.PerformanceReview)),
		("DEPT_CHANGE",      x => x.Events.Any(e => e.Type == CareerEventType.DepartmentChange)),

		("FIRST_TRAINING",   x => x.Events.Any(e => e.Type == CareerEventType.Training)),
		("TEN_TRAININGS",    x => x.Events.Count(e => e.Type == CareerEventType.Training) >= 10),
		("FIRST_CERT",       x => x.Events.Any(e => e.Type == CareerEventType.Certification)),
		("FIVE_CERTS",       x => x.Events.Count(e => e.Type == CareerEventType.Certification) >= 5),
		("TEN_CERTS",        x => x.Events.Count(e => e.Type == CareerEventType.Certification) >= 10),

		("STREAK_7",         x => x.Collaborator.CurrentStreakDays >= 7   || x.Collaborator.LongestStreakDays >= 7),
		("STREAK_30",        x => x.Collaborator.CurrentStreakDays >= 30  || x.Collaborator.LongestStreakDays >= 30),
		("STREAK_90",        x => x.Collaborator.CurrentStreakDays >= 90  || x.Collaborator.LongestStreakDays >= 90),
		("STREAK_365",       x => x.Collaborator.CurrentStreakDays >= 365 || x.Collaborator.LongestStreakDays >= 365),

		("FIRST_KUDOS",      x => x.Events.Any(e => e.Type == CareerEventType.Kudos)),
		("TEN_KUDOS",        x => x.Events.Count(e => e.Type == CareerEventType.Kudos) >= 10),
		("MANAGER",          x => x.Collaborator.DirectReports.Any(r => !r.IsDeleted)),

		// Login & profile
		("FIRST_LOGIN",      x => x.Collaborator.LastLoginAt.HasValue),
		("LOGIN_7",          x => x.Collaborator.CurrentLoginStreakDays >= 7   || x.Collaborator.LongestLoginStreakDays >= 7),
		("LOGIN_30",         x => x.Collaborator.CurrentLoginStreakDays >= 30  || x.Collaborator.LongestLoginStreakDays >= 30),
		("LOGIN_90",         x => x.Collaborator.CurrentLoginStreakDays >= 90  || x.Collaborator.LongestLoginStreakDays >= 90),
		("LOGIN_365",        x => x.Collaborator.CurrentLoginStreakDays >= 365 || x.Collaborator.LongestLoginStreakDays >= 365),
		("PROFILE_COMPLETE", x => !string.IsNullOrWhiteSpace(x.Collaborator.PhotoUrl)
		                          && !string.IsNullOrWhiteSpace(x.Collaborator.Bio)
		                          && x.Collaborator.Birthdate.HasValue),

		// Kanban
		("FIRST_CARD_DONE",  x => x.KanbanCardsCompleted >= 1),
		("WORKHORSE",        x => x.KanbanCardsCompletedLast7Days >= 10),
		("EPIC_COMPLETE",    x => x.KanbanEpicsCompleted >= 1),
		("THOROUGH",         x => x.KanbanCommentsAuthored >= 10),
	];

	private static int TenureDays(Collaborator c)
	{
		var end = c.TerminationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
		return Math.Max(0, end.DayNumber - c.JoinDate.DayNumber);
	}
}

// preserve compatibility with code that uses the old generic `Card` type name
internal static class _CardTypeAlias { internal static Type _ = typeof(Card); }
