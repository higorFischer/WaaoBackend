using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions;
using Waao.Services.Abstractions.Dtos.Team;
using Waao.Services.Services.Team;
using Waao.Services.Validation.Team;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Team;

public class ManagerNoteServiceTests
{
	private static ManagerNoteService Build(WaaoDbContext db)
		=> new(db, new CreateManagerNoteValidator(), new UpdateManagerNoteValidator());

	private static async Task<Collaborator> Seed(WaaoDbContext db, string name, CollaboratorRoleKind role = CollaboratorRoleKind.Collaborator, Guid? managerId = null)
	{
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = name,
			Email = $"{Guid.NewGuid()}@example.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			RoleKind = role,
			ManagerId = managerId,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();
		return c;
	}

	[Fact]
	public async Task Manager_CanCreateAndReadNotes_ForDirectReport()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var mgr = await Seed(db, "Mgr");
		var report = await Seed(db, "Report", managerId: mgr.Id);

		var note = await svc.CreateAsync(report.Id, new CreateManagerNoteDto { Body = "Doing great", Pinned = true }, mgr.Id);
		note.AuthorId.Should().Be(mgr.Id);
		note.AuthorName.Should().Be("Mgr");

		var list = await svc.GetForCollaboratorAsync(report.Id, mgr.Id);
		list.Should().ContainSingle().Which.Body.Should().Be("Doing great");
	}

	[Fact]
	public async Task Subject_CannotReadOwnNotes_Returns403()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var admin = await Seed(db, "Admin", CollaboratorRoleKind.Admin);
		var report = await Seed(db, "Report");
		await svc.CreateAsync(report.Id, new CreateManagerNoteDto { Body = "Private" }, admin.Id);

		var act = async () => await svc.GetForCollaboratorAsync(report.Id, report.Id);
		await act.Should().ThrowAsync<ForbiddenAccessException>();
	}

	[Fact]
	public async Task Peer_CannotReadNotes_Returns403()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var peer = await Seed(db, "Peer");
		var target = await Seed(db, "Target");

		var act = async () => await svc.GetForCollaboratorAsync(target.Id, peer.Id);
		await act.Should().ThrowAsync<ForbiddenAccessException>();
	}

	[Fact]
	public async Task Hr_CanReadAnyonesNotes()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var hr = await Seed(db, "HR", CollaboratorRoleKind.HR);
		var target = await Seed(db, "Target");
		await svc.CreateAsync(target.Id, new CreateManagerNoteDto { Body = "Note" }, hr.Id);

		var list = await svc.GetForCollaboratorAsync(target.Id, hr.Id);
		list.Should().ContainSingle();
	}

	[Fact]
	public async Task NonAuthorNonStaff_CannotEditNote_Returns403()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var mgr = await Seed(db, "Mgr");
		var report = await Seed(db, "Report", managerId: mgr.Id);
		var note = await svc.CreateAsync(report.Id, new CreateManagerNoteDto { Body = "v1" }, mgr.Id);

		var otherMgr = await Seed(db, "Other");
		var act = async () => await svc.UpdateAsync(note.Id, new UpdateManagerNoteDto { Body = "hacked" }, otherMgr.Id);
		await act.Should().ThrowAsync<ForbiddenAccessException>();
	}
}

public class SkillServiceTests
{
	private static SkillService Build(WaaoDbContext db)
		=> new(db, new CreateSkillValidator(), new UpdateSkillValidator(), new UpsertCollaboratorSkillValidator());

	private static async Task<Collaborator> Seed(WaaoDbContext db, string name, CollaboratorRoleKind role = CollaboratorRoleKind.Collaborator, Guid? managerId = null)
	{
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = name,
			Email = $"{Guid.NewGuid()}@example.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			RoleKind = role,
			ManagerId = managerId,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();
		return c;
	}

	[Fact]
	public async Task Person_CanReadOwnSkills()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var hr = await Seed(db, "HR", CollaboratorRoleKind.HR);
		var person = await Seed(db, "Person");
		var skill = await svc.CreateAsync(new CreateSkillDto { Name = "C#", Category = "Backend" });
		await svc.UpsertForCollaboratorAsync(person.Id, skill.Id, new UpsertCollaboratorSkillDto { Level = SkillLevel.Expert }, hr.Id);

		var own = await svc.GetForCollaboratorAsync(person.Id, person.Id);
		own.Should().ContainSingle().Which.Level.Should().Be(SkillLevel.Expert);
	}

	[Fact]
	public async Task Peer_CannotUpsertOthersSkills_Returns403()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var peer = await Seed(db, "Peer");
		var target = await Seed(db, "Target");
		var skill = await svc.CreateAsync(new CreateSkillDto { Name = "Go" });

		var act = async () => await svc.UpsertForCollaboratorAsync(target.Id, skill.Id, new UpsertCollaboratorSkillDto { Level = SkillLevel.Competent }, peer.Id);
		await act.Should().ThrowAsync<ForbiddenAccessException>();
	}

	[Fact]
	public async Task Manager_Upsert_IsIdempotent_OnSameSkill()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var mgr = await Seed(db, "Mgr");
		var report = await Seed(db, "Report", managerId: mgr.Id);
		var skill = await svc.CreateAsync(new CreateSkillDto { Name = "SQL" });

		await svc.UpsertForCollaboratorAsync(report.Id, skill.Id, new UpsertCollaboratorSkillDto { Level = SkillLevel.Beginner }, mgr.Id);
		var second = await svc.UpsertForCollaboratorAsync(report.Id, skill.Id, new UpsertCollaboratorSkillDto { Level = SkillLevel.Proficient }, mgr.Id);

		second.Level.Should().Be(SkillLevel.Proficient);
		var all = await svc.GetForCollaboratorAsync(report.Id, mgr.Id);
		all.Should().ContainSingle();
		all[0].AssessedById.Should().Be(mgr.Id);
	}
}
