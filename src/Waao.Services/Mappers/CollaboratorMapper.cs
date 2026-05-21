using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Mappers;

public static class CollaboratorMapper
{
	public static CollaboratorDto ToDto(Collaborator c) => new()
	{
		Id = c.Id,
		FullName = c.FullName,
		Email = c.Email,
		Cpf = c.Cpf,
		Birthdate = c.Birthdate,
		JoinDate = c.JoinDate,
		TerminationDate = c.TerminationDate,
		PhotoUrl = c.PhotoUrl,
		Bio = c.Bio,
		Status = c.Status,
		DepartmentId = c.DepartmentId,
		DepartmentName = c.Department?.Name,
		RoleId = c.RoleId,
		RoleTitle = c.Role?.Title,
		ManagerId = c.ManagerId,
		ManagerName = c.Manager?.FullName,
		TotalXp = c.TotalXp,
		CurrentLevel = c.CurrentLevel,
		CurrentStreakDays = c.CurrentStreakDays,
		LongestStreakDays = c.LongestStreakDays,
		BadgeCount = c.Badges?.Count ?? 0,
		RoleKind = c.RoleKind,
		IsDeleted = c.IsDeleted,
	};

	public static void Apply(Collaborator entity, CreateCollaboratorDto dto)
	{
		entity.FullName = dto.FullName;
		entity.Email = dto.Email;
		entity.Cpf = dto.Cpf;
		entity.Birthdate = dto.Birthdate;
		entity.JoinDate = dto.JoinDate;
		entity.PhotoUrl = dto.PhotoUrl;
		entity.Bio = dto.Bio;
		entity.DepartmentId = dto.DepartmentId;
		entity.RoleId = dto.RoleId;
		entity.ManagerId = dto.ManagerId;
	}

	public static void Apply(Collaborator entity, UpdateCollaboratorDto dto)
	{
		entity.FullName = dto.FullName;
		entity.Email = dto.Email;
		entity.Birthdate = dto.Birthdate;
		entity.PhotoUrl = dto.PhotoUrl;
		entity.Bio = dto.Bio;
		entity.DepartmentId = dto.DepartmentId;
		entity.RoleId = dto.RoleId;
		entity.ManagerId = dto.ManagerId;
		entity.Status = dto.Status;
		entity.TerminationDate = dto.TerminationDate;
		entity.OptInLeaderboards = dto.OptInLeaderboards;
	}
}

public static class CareerEventMapper
{
	public static CareerEventDto ToDto(CareerEvent e) => new()
	{
		Id = e.Id,
		CollaboratorId = e.CollaboratorId,
		Type = e.Type,
		EventDate = e.EventDate,
		Title = e.Title,
		Notes = e.Notes,
		FromValue = e.FromValue,
		ToValue = e.ToValue,
		XpAwarded = e.XpAwarded,
		AttachmentUrl = e.AttachmentUrl,
	};
}
