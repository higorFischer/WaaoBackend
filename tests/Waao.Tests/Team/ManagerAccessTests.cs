using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Services.Team;
using Xunit;

namespace Waao.Tests.Team;

public class ManagerAccessTests
{
	private static Collaborator Person(CollaboratorRoleKind role = CollaboratorRoleKind.Collaborator, Guid? managerId = null)
		=> new()
		{
			Id = Guid.CreateVersion7(),
			RoleKind = role,
			ManagerId = managerId,
		};

	[Fact]
	public void IsStaff_True_ForHrAndAdmin()
	{
		ManagerAccess.IsStaff(Person(CollaboratorRoleKind.HR)).Should().BeTrue();
		ManagerAccess.IsStaff(Person(CollaboratorRoleKind.Admin)).Should().BeTrue();
		ManagerAccess.IsStaff(Person(CollaboratorRoleKind.Collaborator)).Should().BeFalse();
	}

	[Fact]
	public void HrCanManageAnyone()
	{
		var hr = Person(CollaboratorRoleKind.HR);
		var target = Person();
		ManagerAccess.CanManage(hr, target).Should().BeTrue();
	}

	[Fact]
	public void AdminCanManageAnyone()
	{
		var admin = Person(CollaboratorRoleKind.Admin);
		var target = Person();
		ManagerAccess.CanManage(admin, target).Should().BeTrue();
	}

	[Fact]
	public void ManagerCanManageDirectReport()
	{
		var mgr = Person();
		var target = Person(managerId: mgr.Id);
		ManagerAccess.CanManage(mgr, target).Should().BeTrue();
	}

	[Fact]
	public void PeerCannotManage()
	{
		var peer = Person();
		var target = Person();
		ManagerAccess.CanManage(peer, target).Should().BeFalse();
	}

	[Fact]
	public void ManagerCanReadDirectReportNotes()
	{
		var mgr = Person();
		var target = Person(managerId: mgr.Id);
		ManagerAccess.CanReadManagerNotes(mgr, target).Should().BeTrue();
	}

	[Fact]
	public void HrCanReadOthersNotes()
	{
		var hr = Person(CollaboratorRoleKind.HR);
		var target = Person();
		ManagerAccess.CanReadManagerNotes(hr, target).Should().BeTrue();
	}

	[Fact]
	public void SubjectCannotReadOwnManagerNotes_EvenWhenStaff()
	{
		var self = Person(CollaboratorRoleKind.Admin);
		ManagerAccess.CanReadManagerNotes(self, self).Should().BeFalse();
	}

	[Fact]
	public void SubjectCanManageOwnSkillsButNotReadOwnNotes()
	{
		// CanManage(self, self) is false for a plain collaborator; skills "read own" is
		// handled separately at the endpoint (caller.Id == target.Id). What we assert
		// here is the security invariant: a subject NEVER reads their own manager notes.
		var self = Person();
		ManagerAccess.CanReadManagerNotes(self, self).Should().BeFalse();
	}
}
