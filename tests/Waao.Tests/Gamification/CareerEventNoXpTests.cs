using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Services.Validation;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public sealed class CareerEventNoXpTests
{
	[Fact]
	public async Task CreatingCareerEvent_RecordsEvent_With0Xp_AndNoLevelChange()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLevel = 0,
			OnboardingCompletedAt = DateTime.UtcNow,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var streaks = new StreakTracker(db);
		var badges = new BadgeEvaluator(db);
		var validator = new CreateCareerEventValidator();
		var svc = new CareerEventService(db, streaks, badges, validator);

		var result = await svc.CreateAsync(new CreateCareerEventDto
		{
			CollaboratorId = c.Id,
			Type = CareerEventType.Training,
			EventDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Title = "Did a course",
		});

		result.XpAwarded.Should().Be(0);
		// No streak-threshold seeds in TestDb → bonus must be 0
		result.StreakBonusXp.Should().Be(0);
		result.LevelBefore.Should().Be(0);
		result.LevelAfter.Should().Be(0);
		(await db.XpTransactions.CountAsync()).Should().Be(0);
	}
}
