using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Services.Services;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class LevelProgressTests
{
	[Fact]
	public async Task ZeroXp_ReportsLevel0()
	{
		using var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });

		var collab = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "Zero Xp",
			Email = "zero@waao.com.br",
			TotalXp = 0,
		};
		db.Collaborators.Add(collab);
		await db.SaveChangesAsync();

		var service = new GamificationService(db);

		var progress = await service.GetLevelProgressAsync(collab.Id);

		progress.CurrentLevel.Should().Be(0);
		progress.CurrentTitle.Should().Be("Unranked");
		progress.ProgressPercent.Should().Be(0);
		progress.CurrentXp.Should().Be(0);
		progress.XpForNextLevel.Should().Be(100);
		progress.NextLevel.Should().Be(2);
	}

	[Fact]
	public async Task HundredXp_ReportsLevel2()
	{
		using var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });

		var collab = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "Hundred Xp",
			Email = "hundred@waao.com.br",
			TotalXp = 100,
		};
		db.Collaborators.Add(collab);
		await db.SaveChangesAsync();

		var service = new GamificationService(db);

		var progress = await service.GetLevelProgressAsync(collab.Id);

		progress.CurrentLevel.Should().Be(2);
	}
}
