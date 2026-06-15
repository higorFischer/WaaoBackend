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
	public void IsAdmin_True_OnlyForAdmin()
	{
		ManagerAccess.IsAdmin(Person(CollaboratorRoleKind.Admin)).Should().BeTrue();
		ManagerAccess.IsAdmin(Person(CollaboratorRoleKind.HR)).Should().BeFalse();
		ManagerAccess.IsAdmin(Person(CollaboratorRoleKind.Collaborator)).Should().BeFalse();
	}

	[Fact]
	public void AdminCanManageAnyone()
	{
		ManagerAccess.CanManage(Person(CollaboratorRoleKind.Admin), Person()).Should().BeTrue();
	}

	[Fact]
	public void HrCannotManage()
	{
		ManagerAccess.CanManage(Person(CollaboratorRoleKind.HR), Person()).Should().BeFalse();
	}

	[Fact]
	public void ManagerCannotManageDirectReport()
	{
		var mgr = Person();
		var target = Person(managerId: mgr.Id);
		ManagerAccess.CanManage(mgr, target).Should().BeFalse();
	}

	[Fact]
	public void PeerCannotManage()
	{
		ManagerAccess.CanManage(Person(), Person()).Should().BeFalse();
	}

	[Fact]
	public void AdminCanReadAnyoneNotes()
	{
		ManagerAccess.CanReadManagerNotes(Person(CollaboratorRoleKind.Admin), Person()).Should().BeTrue();
	}

	[Fact]
	public void HrCannotReadNotes()
	{
		ManagerAccess.CanReadManagerNotes(Person(CollaboratorRoleKind.HR), Person()).Should().BeFalse();
	}

	[Fact]
	public void ManagerCannotReadDirectReportNotes()
	{
		var mgr = Person();
		var target = Person(managerId: mgr.Id);
		ManagerAccess.CanReadManagerNotes(mgr, target).Should().BeFalse();
	}

	[Fact]
	public void NonAdminSubjectCannotReadOwnNotes()
	{
		var self = Person();
		ManagerAccess.CanReadManagerNotes(self, self).Should().BeFalse();
	}
}
