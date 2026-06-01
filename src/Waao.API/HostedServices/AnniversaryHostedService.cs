using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;

namespace Waao.API.HostedServices;

/// <summary>
/// Runs once a day at ~09:00 UTC. Awards XP + emits notifications for
/// collaborators whose birthday or work-anniversary is "today".
/// Idempotent per-day: a small marker entity prevents double-firing if the
/// service restarts mid-day.
/// </summary>
public sealed class AnniversaryHostedService(IServiceScopeFactory ScopeFactory, ILogger<AnniversaryHostedService> Logger) : BackgroundService
{
	private const int BirthdayXp = 50;
	private const int AnniversaryXpPerYear = 100;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Initial delay: wait until the next 09:00 UTC, then loop every 24h.
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await RunOnceAsync(stoppingToken);
			}
			catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
			{
				Logger.LogError(ex, "Anniversary tick failed; will retry tomorrow.");
			}

			var now = DateTime.UtcNow;
			var nextRun = now.Date.AddHours(9);
			if (nextRun <= now) nextRun = nextRun.AddDays(1);
			var delay = nextRun - now;
			Logger.LogInformation("Anniversary service sleeping until {NextRun}.", nextRun);
			await Task.Delay(delay, stoppingToken);
		}
	}

	private async Task RunOnceAsync(CancellationToken ct)
	{
		using var scope = ScopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<WaaoDbContext>();
		var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
		var gamification = scope.ServiceProvider.GetRequiredService<GamificationEngine>();

		var today = DateOnly.FromDateTime(DateTime.UtcNow);

		// Idempotency marker: skip if we already ran today.
		var markerKey = $"anniversary:{today:yyyy-MM-dd}";
		var alreadyRan = await db.XpTransactions.AnyAsync(x => x.Reason == markerKey, ct);
		if (alreadyRan)
		{
			Logger.LogInformation("Anniversary already processed for {Today}.", today);
			return;
		}

		var collaborators = await db.Collaborators
			.AsNoTracking()
			.Where(c => !c.IsDeleted && c.Status != CollaboratorStatus.Terminated)
			.ToListAsync(ct);

		var birthdayPeople = collaborators.Where(c => IsSameMonthDay(c.Birthdate, today)).ToList();
		var anniversaryPeople = collaborators
			.Where(c => IsSameMonthDay(c.JoinDate, today) && c.JoinDate < today)
			.Select(c => new { Collaborator = c, Years = today.Year - c.JoinDate.Year })
			.ToList();

		Logger.LogInformation("Anniversary tick {Today}: {Birthdays} birthdays, {Anniversaries} work anniversaries.",
			today, birthdayPeople.Count, anniversaryPeople.Count);

		foreach (var c in birthdayPeople)
		{
			await gamification.RecordAsync(c.Id, BirthdayXp, XpSource.Admin, "Birthday 🎂", c.Id, "celebration", ct);
			await notifications.CreateAsync(c.Id, NotificationKind.SystemAnnouncement,
				"Feliz aniversário! 🎂",
				$"Você ganhou {BirthdayXp} XP de presente da WAAO.",
				"celebration", c.Id, null, ct);
		}

		foreach (var entry in anniversaryPeople)
		{
			var xp = AnniversaryXpPerYear * entry.Years;
			await gamification.RecordAsync(entry.Collaborator.Id, xp, XpSource.Admin, $"Work anniversary — {entry.Years}y 🎉", entry.Collaborator.Id, "celebration", ct);
			await notifications.CreateAsync(entry.Collaborator.Id, NotificationKind.SystemAnnouncement,
				$"{entry.Years} ano(s) de WAAO! 🎉",
				$"Você ganhou {xp} XP por completar {entry.Years} ano(s) com a gente.",
				"celebration", entry.Collaborator.Id, null, ct);
		}

		// Write the idempotency marker as a zero-XP transaction so we never re-fire today.
		// Marker row to make the day idempotent. Stored even when nobody had an anniversary so
		// we don't re-scan thousands of times if the service restarts.
		if (collaborators.Count > 0)
		{
			db.XpTransactions.Add(new Domain.Models.Entities.XpTransaction
			{
				Id = Guid.CreateVersion7(),
				CollaboratorId = collaborators[0].Id,
				Amount = 0,
				Reason = markerKey,
				Source = XpSource.Admin,
				OccurredAt = DateTime.UtcNow,
				CreatedAt = DateTime.UtcNow,
			});
		}
		await db.SaveChangesAsync(ct);
	}

	private static bool IsSameMonthDay(DateOnly? d, DateOnly today)
		=> d.HasValue && d.Value.Month == today.Month && d.Value.Day == today.Day;

	private static bool IsSameMonthDay(DateOnly d, DateOnly today)
		=> d.Month == today.Month && d.Day == today.Day;
}
