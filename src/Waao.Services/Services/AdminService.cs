using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;
using Waao.Services.Mappers;

namespace Waao.Services.Services;

public sealed class AdminService(
	WaaoDbContext Db,
	StreakTracker Streaks,
	BadgeEvaluator Badges) : IAdminService
{
	// =====================================================================
	// PEOPLE
	// =====================================================================

	public async Task<CollaboratorDto> PromoteAsync(Guid collaboratorId, PromoteCollaboratorDto dto, Guid actorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators
			.Include(x => x.Role)
			.Include(x => x.Department)
			.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var fromRole = c.Role?.Title ?? "—";
		var fromDept = c.Department?.Name;

		Role? newRole = null;
		if (dto.RoleId.HasValue)
		{
			newRole = await Db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId, ct)
				?? throw new KeyNotFoundException($"Role {dto.RoleId} not found.");
			c.RoleId = newRole.Id;
		}

		string? newDeptName = fromDept;
		if (dto.DepartmentId.HasValue && dto.DepartmentId != c.DepartmentId)
		{
			var dep = await Db.Departments.FirstOrDefaultAsync(d => d.Id == dto.DepartmentId, ct)
				?? throw new KeyNotFoundException($"Department {dto.DepartmentId} not found.");
			c.DepartmentId = dep.Id;
			newDeptName = dep.Name;
		}

		c.UpdatedAt = DateTime.UtcNow;

		// Log the promotion as a CareerEvent — triggers streak tracking and badge evaluation (XP is admin-granted only).
		var evt = new CareerEvent
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = c.Id,
			Type = CareerEventType.Promotion,
			EventDate = dto.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
			Title = string.IsNullOrWhiteSpace(dto.Title) ? "Promotion" : dto.Title,
			Notes = dto.Notes,
			FromValue = fromRole,
			ToValue = newRole?.Title ?? fromRole,
		};
		Db.CareerEvents.Add(evt);

		// XP is admin-granted only — promotions no longer auto-award XP via career events.
		evt.XpAwarded = 0;
		await Streaks.RegisterActivityAsync(c.Id, evt.EventDate, ct);
		await Db.SaveChangesAsync(ct);
		await Badges.EvaluateAsync(c.Id, ct);
		await Db.SaveChangesAsync(ct);

		_ = actorId;
		_ = newDeptName;
		var saved = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstAsync(x => x.Id == c.Id, ct);
		return CollaboratorMapper.ToDto(saved);
	}

	public async Task<CollaboratorDto> SetRoleKindAsync(Guid collaboratorId, SetRoleKindDto dto, Guid actorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		// Safety: never let the last Admin demote themselves out of Admin.
		if (c.RoleKind == CollaboratorRoleKind.Admin && dto.RoleKind != CollaboratorRoleKind.Admin && c.Id == actorId)
		{
			var anotherAdmin = await Db.Collaborators.AnyAsync(x => x.Id != c.Id && x.RoleKind == CollaboratorRoleKind.Admin, ct);
			if (!anotherAdmin)
				throw new InvalidOperationException("You cannot remove your own Admin role — there are no other admins.");
		}

		c.RoleKind = dto.RoleKind;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var saved = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role).Include(x => x.Manager).Include(x => x.Badges)
			.FirstAsync(x => x.Id == c.Id, ct);
		return CollaboratorMapper.ToDto(saved);
	}

	public async Task<CollaboratorDto> SetCollaboratorRoleAsync(Guid collaboratorId, SetCollaboratorRoleDto dto, Guid actorId, CancellationToken ct = default)
	{
		_ = actorId;
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");
		c.RoleId = dto.RoleId;
		c.DepartmentId = dto.DepartmentId;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var saved = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role).Include(x => x.Manager).Include(x => x.Badges)
			.FirstAsync(x => x.Id == c.Id, ct);
		return CollaboratorMapper.ToDto(saved);
	}

	// =====================================================================
	// JOB ROLES
	// =====================================================================

	public async Task<IReadOnlyList<JobRoleDto>> ListJobRolesAsync(CancellationToken ct = default)
	{
		var counts = await Db.Collaborators
			.Where(c => c.RoleId != null)
			.GroupBy(c => c.RoleId!.Value)
			.Select(g => new { Id = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Id, x => x.Count, ct);

		return await Db.Roles
			.OrderBy(r => r.Track).ThenBy(r => r.SeniorityOrder).ThenBy(r => r.Title)
			.Select(r => new JobRoleDto
			{
				Id = r.Id, Title = r.Title, Track = r.Track,
				SeniorityOrder = r.SeniorityOrder, Description = r.Description,
				CollaboratorCount = counts.GetValueOrDefault(r.Id, 0),
			})
			.ToListAsync(ct);
	}

	public async Task<JobRoleDto> CreateJobRoleAsync(CreateJobRoleDto dto, CancellationToken ct = default)
	{
		var entity = new Role
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title, Track = dto.Track,
			SeniorityOrder = dto.SeniorityOrder, Description = dto.Description,
		};
		Db.Roles.Add(entity);
		await Db.SaveChangesAsync(ct);
		return MapRole(entity, 0);
	}

	public async Task<JobRoleDto> UpdateJobRoleAsync(Guid id, UpdateJobRoleDto dto, CancellationToken ct = default)
	{
		var role = await Db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct)
			?? throw new KeyNotFoundException($"Role {id} not found.");
		role.Title = dto.Title;
		role.Track = dto.Track;
		role.SeniorityOrder = dto.SeniorityOrder;
		role.Description = dto.Description;
		role.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		var count = await Db.Collaborators.CountAsync(c => c.RoleId == role.Id, ct);
		return MapRole(role, count);
	}

	public async Task DeleteJobRoleAsync(Guid id, CancellationToken ct = default)
	{
		var role = await Db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct)
			?? throw new KeyNotFoundException($"Role {id} not found.");
		var inUse = await Db.Collaborators.AnyAsync(c => c.RoleId == role.Id, ct);
		if (inUse) throw new InvalidOperationException("This role is assigned to one or more collaborators. Reassign them first.");
		role.IsDeleted = true;
		role.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// DEPARTMENTS
	// =====================================================================

	public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(CancellationToken ct = default)
	{
		var counts = await Db.Collaborators
			.Where(c => c.DepartmentId != null)
			.GroupBy(c => c.DepartmentId!.Value)
			.Select(g => new { Id = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Id, x => x.Count, ct);

		return await Db.Departments
			.OrderBy(d => d.Name)
			.Select(d => new DepartmentDto
			{
				Id = d.Id, Name = d.Name, Description = d.Description, ColorHex = d.ColorHex,
				CollaboratorCount = counts.GetValueOrDefault(d.Id, 0),
			})
			.ToListAsync(ct);
	}

	public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto, CancellationToken ct = default)
	{
		var entity = new Department { Id = Guid.CreateVersion7(), Name = dto.Name, Description = dto.Description, ColorHex = dto.ColorHex };
		Db.Departments.Add(entity);
		await Db.SaveChangesAsync(ct);
		return new DepartmentDto { Id = entity.Id, Name = entity.Name, Description = entity.Description, ColorHex = entity.ColorHex };
	}

	public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto, CancellationToken ct = default)
	{
		var entity = await Db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
			?? throw new KeyNotFoundException($"Department {id} not found.");
		entity.Name = dto.Name; entity.Description = dto.Description; entity.ColorHex = dto.ColorHex;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		var count = await Db.Collaborators.CountAsync(c => c.DepartmentId == entity.Id, ct);
		return new DepartmentDto { Id = entity.Id, Name = entity.Name, Description = entity.Description, ColorHex = entity.ColorHex, CollaboratorCount = count };
	}

	public async Task DeleteDepartmentAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
			?? throw new KeyNotFoundException($"Department {id} not found.");
		var inUse = await Db.Collaborators.AnyAsync(c => c.DepartmentId == entity.Id, ct);
		if (inUse) throw new InvalidOperationException("This department has assigned collaborators. Reassign them first.");
		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// LEVELS
	// =====================================================================

	public async Task<IReadOnlyList<LevelDefinitionDto>> ListLevelsAsync(CancellationToken ct = default)
		=> await Db.LevelDefinitions
			.OrderBy(l => l.Level)
			.Select(l => new LevelDefinitionDto
			{
				Id = l.Id, Level = l.Level, XpThreshold = l.XpThreshold,
				Title = l.Title, IconEmoji = l.IconEmoji, ColorHex = l.ColorHex,
			})
			.ToListAsync(ct);

	public async Task<LevelDefinitionDto> UpsertLevelAsync(UpsertLevelDefinitionDto dto, CancellationToken ct = default)
	{
		var existing = await Db.LevelDefinitions.FirstOrDefaultAsync(l => l.Level == dto.Level, ct);
		if (existing is null)
		{
			existing = new LevelDefinition { Id = Guid.CreateVersion7(), Level = dto.Level };
			Db.LevelDefinitions.Add(existing);
		}
		existing.XpThreshold = dto.XpThreshold;
		existing.Title = dto.Title;
		existing.IconEmoji = dto.IconEmoji;
		existing.ColorHex = dto.ColorHex;
		existing.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return new LevelDefinitionDto
		{
			Id = existing.Id, Level = existing.Level, XpThreshold = existing.XpThreshold,
			Title = existing.Title, IconEmoji = existing.IconEmoji, ColorHex = existing.ColorHex,
		};
	}

	public async Task DeleteLevelAsync(Guid id, CancellationToken ct = default)
	{
		var l = await Db.LevelDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct)
			?? throw new KeyNotFoundException($"Level {id} not found.");
		l.IsDeleted = true;
		l.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// ----- helpers -----

	private static JobRoleDto MapRole(Role r, int count) => new()
	{
		Id = r.Id, Title = r.Title, Track = r.Track,
		SeniorityOrder = r.SeniorityOrder, Description = r.Description,
		CollaboratorCount = count,
	};
}
