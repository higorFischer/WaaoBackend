using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;
using Waao.Services.Mappers;

namespace Waao.Services.Services;

public sealed class CollaboratorService(
	WaaoDbContext Db,
	BadgeEvaluator Badges,
	IValidator<CreateCollaboratorDto> CreateValidator,
	IValidator<UpdateCollaboratorDto> UpdateValidator) : ICollaboratorService
{
	private IQueryable<Collaborator> Query() => Db.Collaborators
		.Include(c => c.Department)
		.Include(c => c.Role)
		.Include(c => c.Manager)
		.Include(c => c.Badges)
		.AsQueryable();

	public async Task<IReadOnlyList<CollaboratorDto>> GetAllAsync(CancellationToken ct = default)
		=> await Query().OrderBy(c => c.FullName)
			.Select(c => new CollaboratorDto
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
				DepartmentName = c.Department != null ? c.Department.Name : null,
				RoleId = c.RoleId,
				RoleTitle = c.Role != null ? c.Role.Title : null,
				ManagerId = c.ManagerId,
				ManagerName = c.Manager != null ? c.Manager.FullName : null,
				TotalXp = c.TotalXp,
				CurrentLevel = c.CurrentLevel,
				CurrentStreakDays = c.CurrentStreakDays,
				LongestStreakDays = c.LongestStreakDays,
				BadgeCount = c.Badges.Count,
				RoleKind = c.RoleKind,
			})
			.ToListAsync(ct);

	public async Task<CollaboratorDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
	{
		var c = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
		return c is null ? null : CollaboratorMapper.ToDto(c);
	}

	public async Task<CollaboratorDto> CreateAsync(CreateCollaboratorDto dto, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var entity = new Collaborator { Id = Guid.CreateVersion7() };
		CollaboratorMapper.Apply(entity, dto);
		Db.Collaborators.Add(entity);
		await Db.SaveChangesAsync(ct);

		// Day-one badges: WELCOME plus any tenure badge if they joined in the past
		await Badges.EvaluateAsync(entity.Id, ct);
		await Db.SaveChangesAsync(ct);

		var saved = await Query().FirstAsync(x => x.Id == entity.Id, ct);
		return CollaboratorMapper.ToDto(saved);
	}

	public async Task<CollaboratorDto> UpdateAsync(Guid id, UpdateCollaboratorDto dto, CancellationToken ct = default)
	{
		await UpdateValidator.ValidateAndThrowAsync(dto, ct);

		var entity = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Collaborator {id} not found");
		CollaboratorMapper.Apply(entity, dto);
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		// Re-evaluate — manager changes can unlock MANAGER, tenure may now qualify, etc.
		await Badges.EvaluateAsync(entity.Id, ct);
		await Db.SaveChangesAsync(ct);

		var saved = await Query().FirstAsync(x => x.Id == entity.Id, ct);
		return CollaboratorMapper.ToDto(saved);
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Collaborator {id} not found");
		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
}
