using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Team;
using Waao.Infra.EF;
using Waao.Services.Abstractions;
using Waao.Services.Abstractions.Dtos.Team;
using Waao.Services.Abstractions.Services;
using Waao.Services.Mappers.Team;

namespace Waao.Services.Services.Team;

public sealed class ManagerNoteService(
	WaaoDbContext Db,
	IValidator<CreateManagerNoteDto> CreateValidator,
	IValidator<UpdateManagerNoteDto> UpdateValidator) : IManagerNoteService
{
	public async Task<IReadOnlyList<ManagerNoteDto>> GetForCollaboratorAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default)
	{
		var (caller, target) = await LoadPairAsync(callerId, collaboratorId, ct);
		EnsureCanReadNotes(caller, target);

		return await Db.ManagerNotes
			.Where(n => n.CollaboratorId == collaboratorId)
			.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.CreatedAt)
			.Select(n => ManagerNoteMapper.ToDto(n))
			.ToListAsync(ct);
	}

	public async Task<ManagerNoteDto> CreateAsync(Guid collaboratorId, CreateManagerNoteDto dto, Guid callerId, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var (caller, target) = await LoadPairAsync(callerId, collaboratorId, ct);
		EnsureCanReadNotes(caller, target);

		var entity = new ManagerNote
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = collaboratorId,
			AuthorId = caller.Id,
			AuthorName = caller.FullName,
			Body = dto.Body.Trim(),
			Pinned = dto.Pinned,
		};
		Db.ManagerNotes.Add(entity);
		await Db.SaveChangesAsync(ct);
		return ManagerNoteMapper.ToDto(entity);
	}

	public async Task<ManagerNoteDto> UpdateAsync(Guid id, UpdateManagerNoteDto dto, Guid callerId, CancellationToken ct = default)
	{
		await UpdateValidator.ValidateAndThrowAsync(dto, ct);

		var entity = await Db.ManagerNotes.FirstOrDefaultAsync(n => n.Id == id, ct)
			?? throw new KeyNotFoundException($"Manager note {id} not found");
		var caller = await RequireCollaboratorAsync(callerId, ct);
		EnsureAuthorOrStaff(caller, entity);

		entity.Body = dto.Body.Trim();
		entity.Pinned = dto.Pinned;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ManagerNoteMapper.ToDto(entity);
	}

	public async Task DeleteAsync(Guid id, Guid callerId, CancellationToken ct = default)
	{
		var entity = await Db.ManagerNotes.FirstOrDefaultAsync(n => n.Id == id, ct)
			?? throw new KeyNotFoundException($"Manager note {id} not found");
		var caller = await RequireCollaboratorAsync(callerId, ct);
		EnsureAuthorOrStaff(caller, entity);

		entity.IsDeleted = true;
		entity.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	private static void EnsureCanReadNotes(Collaborator caller, Collaborator target)
	{
		if (!ManagerAccess.CanReadManagerNotes(caller, target))
			throw new ForbiddenAccessException("You are not allowed to access this collaborator's manager notes.");
	}

	private static void EnsureAuthorOrStaff(Collaborator caller, ManagerNote note)
	{
		if (note.AuthorId != caller.Id && !ManagerAccess.IsStaff(caller))
			throw new ForbiddenAccessException("Only the author or HR/Admin can modify this manager note.");
	}

	private async Task<(Collaborator Caller, Collaborator Target)> LoadPairAsync(Guid callerId, Guid targetId, CancellationToken ct)
	{
		var caller = await RequireCollaboratorAsync(callerId, ct);
		var target = callerId == targetId
			? caller
			: await RequireCollaboratorAsync(targetId, ct);
		return (caller, target);
	}

	private async Task<Collaborator> RequireCollaboratorAsync(Guid id, CancellationToken ct)
		=> await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Collaborator {id} not found");
}
