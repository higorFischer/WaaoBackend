using FluentAssertions;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Services.Allocation;
using Waao.Services.Validation.Allocation;
using Waao.Tests.Support;

namespace Waao.Tests.Allocation;

public class AllocationServiceTests
{
	private static AllocationService Build(Waao.Infra.EF.WaaoDbContext db) =>
		new(db, new CreateProjectValidator(), new UpdateProjectValidator(),
			new CreateAllocationValidator(), new UpdateNoteValidator());

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name)
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id, FullName = name, Email = $"{id}@example.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
		});
		await db.SaveChangesAsync();
		return id;
	}

	[Fact]
	public async Task CreateProject_ThenBoard_ReturnsProjectAndAllActiveCollaborators()
	{
		var db = TestDb.New();
		var svc = Build(db);
		await SeedCollaborator(db, "Alice");

		await svc.CreateProjectAsync(new CreateProjectDto { Title = "Billing" });
		var board = await svc.GetBoardAsync();

		board.Projects.Should().ContainSingle(p => p.Title == "Billing");
		board.Collaborators.Should().ContainSingle(c => c.FullName == "Alice");
	}

	[Fact]
	public async Task Allocate_SameCollaboratorTwiceToSameProject_IsIdempotent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Bob");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Emergency" });

		var first  = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who, Note = "triage" }, who);
		var second = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who, Note = "still triage" }, who);

		second.Id.Should().Be(first.Id);
		var board = await svc.GetBoardAsync();
		board.Projects.Single().Allocations.Should().ContainSingle();
		board.Projects.Single().Allocations.Single().Note.Should().Be("still triage");
	}

	[Fact]
	public async Task Allocate_SameCollaboratorToTwoProjects_AppearsInBoth()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Cara");
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P1" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P2" });

		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p1.Id, CollaboratorId = who }, who);
		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p2.Id, CollaboratorId = who }, who);

		var board = await svc.GetBoardAsync();
		board.Projects.SelectMany(p => p.Allocations).Should().HaveCount(2);
	}

	[Fact]
	public async Task Move_ToProjectWhereCollaboratorAlreadyExists_MergesNoDuplicate()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Dan");
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P1" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P2" });
		var inP1 = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p1.Id, CollaboratorId = who }, who);
		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p2.Id, CollaboratorId = who }, who);

		await svc.MoveAllocationAsync(inP1.Id, new MoveAllocationDto { ProjectId = p2.Id, Position = 0 });

		var board = await svc.GetBoardAsync();
		board.Projects.SelectMany(p => p.Allocations).Should().ContainSingle();
		board.Projects.Single(p => p.Id == p2.Id).Allocations.Should().ContainSingle();
	}

	[Fact]
	public async Task Allocate_ToArchivedProject_Throws()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Eve");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Old" });
		await svc.ArchiveProjectAsync(proj.Id);

		var act = () => svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who }, who);
		await act.Should().ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task Archive_HidesProjectFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Temp" });

		await svc.ArchiveProjectAsync(proj.Id);

		var board = await svc.GetBoardAsync();
		board.Projects.Should().BeEmpty();
	}

	[Fact]
	public async Task RemoveAllocation_SoftDeletes_DisappearsFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Fay");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P" });
		var alloc = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who }, who);

		await svc.RemoveAllocationAsync(alloc.Id);

		var board = await svc.GetBoardAsync();
		board.Projects.Single().Allocations.Should().BeEmpty();
	}
}
