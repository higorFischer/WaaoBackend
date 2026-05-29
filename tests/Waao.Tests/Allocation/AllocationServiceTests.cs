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
			new CreateAllocationValidator(), new UpdateNoteValidator(),
			new CreateConnectionValidator(), new UpdatePositionValidator());

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

	[Fact]
	public async Task CreateConnection_ThenBoard_ReturnsEdge()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });

		await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id, Label = "depends" });
		var board = await svc.GetBoardAsync();

		board.Connections.Should().ContainSingle(c => c.SourceProjectId == p1.Id && c.TargetProjectId == p2.Id && c.Label == "depends");
	}

	[Fact]
	public async Task CreateConnection_Duplicate_IsIdempotent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });

		var first = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id });
		var second = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id, Label = "x" });

		second.Id.Should().Be(first.Id);
		(await svc.GetBoardAsync()).Connections.Should().ContainSingle();
	}

	[Fact]
	public async Task RemoveConnection_DropsEdgeFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });
		var c = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id });

		await svc.RemoveConnectionAsync(c.Id);

		(await svc.GetBoardAsync()).Connections.Should().BeEmpty();
	}

	[Fact]
	public async Task UpdatePosition_PersistsOnBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });

		await svc.UpdateProjectPositionAsync(p.Id, new UpdatePositionDto { X = 123, Y = 456 });

		var box = (await svc.GetBoardAsync()).Projects.Single(x => x.Id == p.Id);
		box.PositionX.Should().Be(123);
		box.PositionY.Should().Be(456);
	}
}
