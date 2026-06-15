using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Skills;
using Waao.Infra.EF;
using Waao.Services.Abstractions;
using Waao.Services.Abstractions.Dtos.Team;
using Waao.Services.Abstractions.Services;
using Waao.Services.Mappers.Team;

namespace Waao.Services.Services.Team;

public sealed class SkillService(
	WaaoDbContext Db,
	IValidator<CreateSkillDto> CreateValidator,
	IValidator<UpdateSkillDto> UpdateValidator,
	IValidator<UpsertCollaboratorSkillDto> UpsertValidator) : ISkillService
{
	// ----- Catalog -----

	public async Task<IReadOnlyList<SkillDto>> GetCatalogAsync(bool includeArchived = false, CancellationToken ct = default)
		=> await Db.Skills
			.Where(s => includeArchived || !s.IsArchived)
			.OrderBy(s => s.Category).ThenBy(s => s.Name)
			.Select(s => SkillMapper.ToDto(s))
			.ToListAsync(ct);

	public async Task<SkillDto> CreateAsync(CreateSkillDto dto, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var entity = new Skill
		{
			Id = Guid.CreateVersion7(),
			Name = dto.Name.Trim(),
			Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim(),
		};
		Db.Skills.Add(entity);
		await Db.SaveChangesAsync(ct);
		return SkillMapper.ToDto(entity);
	}

	public async Task<SkillDto> UpdateAsync(Guid id, UpdateSkillDto dto, CancellationToken ct = default)
	{
		await UpdateValidator.ValidateAndThrowAsync(dto, ct);

		var entity = await Db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct)
			?? throw new KeyNotFoundException($"Skill {id} not found");

		entity.Name = dto.Name.Trim();
		entity.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
		entity.IsArchived = dto.IsArchived;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return SkillMapper.ToDto(entity);
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct)
			?? throw new KeyNotFoundException($"Skill {id} not found");
		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// ----- Per-collaborator assessments -----

	public async Task<IReadOnlyList<CollaboratorSkillDto>> GetForCollaboratorAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default)
	{
		var (caller, target) = await LoadPairAsync(callerId, collaboratorId, ct);

		// People may always read their OWN skills; otherwise the caller must be able to manage them.
		if (caller.Id != target.Id && !ManagerAccess.CanManage(caller, target))
			throw new ForbiddenAccessException("You are not allowed to view this collaborator's skills.");

		return await Db.CollaboratorSkills
			.Include(cs => cs.Skill)
			.Where(cs => cs.CollaboratorId == collaboratorId)
			.OrderBy(cs => cs.Skill!.Category).ThenBy(cs => cs.Skill!.Name)
			.Select(cs => SkillMapper.ToDto(cs))
			.ToListAsync(ct);
	}

	public async Task<CollaboratorSkillDto> UpsertForCollaboratorAsync(Guid collaboratorId, Guid skillId, UpsertCollaboratorSkillDto dto, Guid callerId, CancellationToken ct = default)
	{
		await UpsertValidator.ValidateAndThrowAsync(dto, ct);

		var (caller, target) = await LoadPairAsync(callerId, collaboratorId, ct);
		if (!ManagerAccess.CanManage(caller, target))
			throw new ForbiddenAccessException("You are not allowed to assess this collaborator's skills.");

		var skill = await Db.Skills.FirstOrDefaultAsync(s => s.Id == skillId, ct)
			?? throw new KeyNotFoundException($"Skill {skillId} not found");

		var entity = await Db.CollaboratorSkills
			.FirstOrDefaultAsync(cs => cs.CollaboratorId == collaboratorId && cs.SkillId == skillId, ct);

		if (entity is null)
		{
			entity = new CollaboratorSkill
			{
				Id = Guid.CreateVersion7(),
				CollaboratorId = collaboratorId,
				SkillId = skillId,
			};
			Db.CollaboratorSkills.Add(entity);
		}

		entity.Level = dto.Level;
		entity.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
		entity.AssessedById = callerId;
		entity.AssessedAt = DateTime.UtcNow;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		entity.Skill = skill;
		return SkillMapper.ToDto(entity);
	}

	public async Task RemoveForCollaboratorAsync(Guid collaboratorId, Guid skillId, Guid callerId, CancellationToken ct = default)
	{
		var (caller, target) = await LoadPairAsync(callerId, collaboratorId, ct);
		if (!ManagerAccess.CanManage(caller, target))
			throw new ForbiddenAccessException("You are not allowed to remove this collaborator's skills.");

		var entity = await Db.CollaboratorSkills
			.FirstOrDefaultAsync(cs => cs.CollaboratorId == collaboratorId && cs.SkillId == skillId, ct)
			?? throw new KeyNotFoundException($"Skill {skillId} is not assessed for collaborator {collaboratorId}");

		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	private async Task<(Collaborator Caller, Collaborator Target)> LoadPairAsync(Guid callerId, Guid targetId, CancellationToken ct)
	{
		var caller = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == callerId, ct)
			?? throw new KeyNotFoundException($"Collaborator {callerId} not found");
		var target = callerId == targetId
			? caller
			: await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == targetId, ct)
				?? throw new KeyNotFoundException($"Collaborator {targetId} not found");
		return (caller, target);
	}
}
