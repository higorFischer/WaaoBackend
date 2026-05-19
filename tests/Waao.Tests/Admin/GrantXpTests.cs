using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Admin;

public sealed class GrantXpTests
{
	private static (Waao.Services.Services.AdminService svc, Waao.Infra.EF.WaaoDbContext db, Collaborator c, Guid admin) Build()
	{
		var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = "T",
			Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLevel = 0,
		};
		db.Collaborators.Add(c);
		db.SaveChanges();
		return (AdminServiceFactory.Create(db), db, c, Guid.CreateVersion7());
	}

	[Fact]
	public async Task GrantXp_Positive_AddsXp_RecomputesLevel_WritesAdminTransaction()
	{
		var (svc, db, c, admin) = Build();
		var dto = await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = 120, Reason = "Q2 project" }, admin);

		dto.TotalXp.Should().Be(120);
		dto.CurrentLevel.Should().Be(2);
		var tx = await db.XpTransactions.SingleAsync();
		tx.Amount.Should().Be(120);
		tx.Source.Should().Be(XpSource.Admin);
		tx.Reason.Should().Be("Q2 project");
	}

	[Fact]
	public async Task GrantXp_Negative_Deducts()
	{
		var (svc, db, c, admin) = Build();
		await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = 100, Reason = "grant" }, admin);
		var dto = await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = -40, Reason = "correction" }, admin);
		dto.TotalXp.Should().Be(60);
	}

	[Fact]
	public async Task GrantXp_MissingCollaborator_Throws()
	{
		var (svc, _, _, admin) = Build();
		var act = async () => await svc.GrantXpAsync(Guid.NewGuid(), new GrantXpDto { Amount = 10, Reason = "x" }, admin);
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}
}
