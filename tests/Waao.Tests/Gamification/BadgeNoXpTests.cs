using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class BadgeNoXpTests
{
	[Fact]
	public async Task FirstLoginBadge_Unlocks_ButGrantsNoXp()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			LastLoginAt = DateTime.UtcNow,
			OnboardingCompletedAt = DateTime.UtcNow,
		};
		db.Collaborators.Add(c);
		db.Badges.Add(new Badge
		{
			Id = Guid.CreateVersion7(),
			Code = "FIRST_LOGIN",
			Name = "Hello WAAO",
			Category = BadgeCategory.Activity,
			Rarity = BadgeRarity.Common,
			XpReward = 50,
			UnlockRule = "First successful login",
		});
		await db.SaveChangesAsync();

		var evaluator = new BadgeEvaluator(db);
		var awarded = await evaluator.EvaluateAsync(c.Id);
		await db.SaveChangesAsync();

		awarded.Select(b => b.Code).Should().Contain("FIRST_LOGIN");
		(await db.XpTransactions.CountAsync()).Should().Be(0);
		(await db.Collaborators.FirstAsync()).TotalXp.Should().Be(0);
	}

	[Fact]
	public async Task NotOnboarded_NoBadgesUnlock()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			LastLoginAt = DateTime.UtcNow,
			OnboardingCompletedAt = null,
		};
		db.Collaborators.Add(c);
		db.Badges.Add(new Badge
		{
			Id = Guid.CreateVersion7(),
			Code = "FIRST_LOGIN",
			Name = "Hello WAAO",
			Category = BadgeCategory.Activity,
			Rarity = BadgeRarity.Common,
			XpReward = 50,
			UnlockRule = "First successful login",
		});
		await db.SaveChangesAsync();

		var evaluator = new BadgeEvaluator(db);
		var awarded = await evaluator.EvaluateAsync(c.Id);
		await db.SaveChangesAsync();

		awarded.Should().BeEmpty();
		(await db.CollaboratorBadges.CountAsync()).Should().Be(0);
	}
}
