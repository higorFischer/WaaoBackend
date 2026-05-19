using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF.Seeds;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Seeds;

public sealed class SeedTests
{
	[Fact]
	public async Task Seed_CreatesOnlyHigorAdmin()
	{
		using var db = TestDb.New();
		await DbInitializer.SeedAsync(db);

		var users = await db.Collaborators.ToListAsync();
		users.Should().ContainSingle();
		users[0].Email.Should().Be("higor@waao.com.br");
		users[0].RoleKind.Should().Be(CollaboratorRoleKind.Admin);
		users[0].OnboardingCompletedAt.Should().NotBeNull();
		users[0].TotalXp.Should().Be(0);
		users[0].CurrentLevel.Should().Be(0);
	}
}
