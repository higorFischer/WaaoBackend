using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class ComputeLevelTests
{
	[Fact]
	public async Task ZeroXp_IsLevel0()
	{
		using var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });
		await db.SaveChangesAsync();
		var engine = new GamificationEngine(db);

		(await engine.ComputeLevelAsync(0)).Should().Be(0);
		(await engine.ComputeLevelAsync(50)).Should().Be(0);
		(await engine.ComputeLevelAsync(99)).Should().Be(0);
		(await engine.ComputeLevelAsync(100)).Should().Be(2);
	}

	[Fact]
	public async Task NoDefinitions_IsLevel0()
	{
		using var db = TestDb.New();
		var engine = new GamificationEngine(db);
		(await engine.ComputeLevelAsync(999)).Should().Be(0);
	}
}
