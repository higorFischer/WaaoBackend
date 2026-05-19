using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class StreakNoXpTests
{
	[Fact]
	public async Task CrossingStreakThreshold_AdvancesStreak_ButGrantsNoXp()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLoginStreakDays = 6,
			LastLoginDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
			OnboardingCompletedAt = DateTime.UtcNow,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var tracker = new StreakTracker(db);
		var (current, _, bonus) = await tracker.RegisterLoginAsync(c.Id);
		await db.SaveChangesAsync();

		current.Should().Be(7);
		bonus.Should().Be(0);
		(await db.XpTransactions.CountAsync()).Should().Be(0);
		(await db.Collaborators.FirstAsync()).TotalXp.Should().Be(0);
	}

	[Fact]
	public async Task NotOnboarded_StreakDoesNotAdvance()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLoginStreakDays = 6,
			LastLoginDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
			OnboardingCompletedAt = null,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var tracker = new StreakTracker(db);
		var (current, longest, bonus) = await tracker.RegisterLoginAsync(c.Id);
		await db.SaveChangesAsync();

		(current, longest, bonus).Should().Be((0, 0, 0));
		(await db.Collaborators.FirstAsync()).CurrentLoginStreakDays.Should().Be(6);
	}

	[Fact]
	public async Task NotOnboarded_ActivityStreakDoesNotAdvance()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentStreakDays = 6,
			LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
			OnboardingCompletedAt = null,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var tracker = new StreakTracker(db);
		var (current, longest, bonus) = await tracker.RegisterActivityAsync(c.Id);
		await db.SaveChangesAsync();

		(current, longest, bonus).Should().Be((0, 0, 0));
		(await db.Collaborators.FirstAsync()).CurrentStreakDays.Should().Be(6);
	}
}
