using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Onboarding;

public sealed class OnboardingServiceTests
{
	private static (Guid CollaboratorId, Guid DepartmentId) Seed(Waao.Infra.EF.WaaoDbContext db)
	{
		var dept = new Department { Id = Guid.CreateVersion7(), Name = "Eng", Description = "", ColorHex = "#000" };
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) };
		db.Add(dept); db.Add(c); db.SaveChanges();
		return (c.Id, dept.Id);
	}

	[Fact]
	public async Task GetStatus_NotOnboarded_AllFlagsFalse()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, _) = Seed(db);
		var s = await svc.GetStatusAsync(cid);
		s.Completed.Should().BeFalse();
		s.CompletedAt.Should().BeNull();
		s.PhotoSet.Should().BeFalse();
		s.BioSet.Should().BeFalse();
		s.BirthdateSet.Should().BeFalse();
		s.DepartmentSet.Should().BeFalse();
	}

	[Fact]
	public async Task Complete_SetsFields_AndOnboardingCompletedAt()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		var s = await svc.CompleteAsync(cid, new CompleteOnboardingDto
		{
			PhotoUrl = "https://x/p.png",
			Bio = "Hello",
			Birthdate = new DateOnly(1990, 1, 1),
			DepartmentId = did,
		});
		s.Completed.Should().BeTrue();
		s.CompletedAt.Should().NotBeNull();
		var c = await db.Collaborators.FirstAsync();
		c.OnboardingCompletedAt.Should().NotBeNull();
		c.PhotoUrl.Should().Be("https://x/p.png");
		c.Bio.Should().Be("Hello");
		c.Birthdate.Should().Be(new DateOnly(1990, 1, 1));
		c.DepartmentId.Should().Be(did);
		c.UpdatedAt.Should().NotBe(default);
	}

	[Fact]
	public async Task Complete_AlreadyCompleted_Idempotent_NoOverwrite()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		var first = await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990, 1, 1), DepartmentId = did });
		var firstAt = first.CompletedAt;
		var snd = await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u2", Bio = "b2", Birthdate = new DateOnly(1991, 1, 1), DepartmentId = did });
		snd.Completed.Should().BeTrue();
		snd.CompletedAt.Should().Be(firstAt);
		var c = await db.Collaborators.FirstAsync();
		c.PhotoUrl.Should().Be("u");
		c.Bio.Should().Be("b");
	}

	[Fact]
	public async Task Complete_UnknownDepartment_Throws()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, _) = Seed(db);
		var act = async () => await svc.CompleteAsync(cid, new CompleteOnboardingDto
		{
			PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990, 1, 1), DepartmentId = Guid.NewGuid(),
		});
		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task Complete_MissingCollaborator_Throws()
	{
		var (svc, _) = OnboardingServiceFactory.Create();
		var act = async () => await svc.CompleteAsync(Guid.NewGuid(), new CompleteOnboardingDto
		{
			PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990, 1, 1), DepartmentId = Guid.NewGuid(),
		});
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}

	[Fact]
	public async Task GetStatus_AfterComplete_ReturnsCompleted()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990, 1, 1), DepartmentId = did });
		var s = await svc.GetStatusAsync(cid);
		s.Completed.Should().BeTrue();
		s.PhotoSet.Should().BeTrue();
		s.BioSet.Should().BeTrue();
		s.BirthdateSet.Should().BeTrue();
		s.DepartmentSet.Should().BeTrue();
	}
}
