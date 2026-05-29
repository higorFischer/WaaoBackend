using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Abstractions.Services;
using Waao.Services.Mappers;

namespace Waao.Services.Services.Allocation;

public sealed class AllocationService(
	WaaoDbContext Db,
	IValidator<CreateProjectDto> CreateProjectValidator,
	IValidator<UpdateProjectDto> UpdateProjectValidator,
	IValidator<CreateAllocationDto> CreateAllocationValidator,
	IValidator<UpdateNoteDto> UpdateNoteValidator) : IAllocationService
{
	public async Task<AllocationBoardDto> GetBoardAsync(CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.AsNoTracking()
			.Where(p => !p.IsArchived)
			.OrderBy(p => p.Position).ThenBy(p => p.Title)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.ToListAsync(ct);

		var collaborators = await Db.Collaborators
			.AsNoTracking()
			.Where(c => c.Status == Domain.Models.Enums.CollaboratorStatus.Active)
			.Include(c => c.Role)
			.Include(c => c.Department)
			.OrderBy(c => c.FullName)
			.ToListAsync(ct);

		return new AllocationBoardDto
		{
			Projects = projects.Select(AllocationMapper.ToDto).ToList(),
			Collaborators = collaborators.Select(AllocationMapper.ToChip).ToList(),
		};
	}

	public async Task<IReadOnlyList<ProjectWithAllocationsDto>> GetByCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.AsNoTracking()
			.Where(p => !p.IsArchived && p.Allocations.Any(a => a.CollaboratorId == collaboratorId))
			.OrderBy(p => p.Position)
			.Include(p => p.Allocations.Where(a => a.CollaboratorId == collaboratorId))
				.ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations.Where(a => a.CollaboratorId == collaboratorId))
				.ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.ToListAsync(ct);

		return projects.Select(AllocationMapper.ToDto).ToList();
	}

	public async Task<ProjectWithAllocationsDto> CreateProjectAsync(CreateProjectDto dto, CancellationToken ct = default)
	{
		await CreateProjectValidator.ValidateAndThrowAsync(dto, ct);

		var maxPos = await Db.Projects.Where(p => !p.IsArchived).Select(p => (int?)p.Position).MaxAsync(ct) ?? -1;

		var project = new Project
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title,
			Description = dto.Description,
			ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#2A6B7E" : dto.ColorHex!,
			Position = maxPos + 1,
		};
		Db.Projects.Add(project);
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(project);
	}

	public async Task<ProjectWithAllocationsDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto, CancellationToken ct = default)
	{
		await UpdateProjectValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		project.Title = dto.Title;
		project.Description = dto.Description;
		project.ColorHex = dto.ColorHex;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(project);
	}

	public async Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
	{
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");
		project.IsArchived = true;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task ReorderProjectsAsync(ReorderProjectsDto dto, CancellationToken ct = default)
	{
		var projects = await Db.Projects.Where(p => dto.OrderedIds.Contains(p.Id)).ToListAsync(ct);
		for (var i = 0; i < dto.OrderedIds.Count; i++)
		{
			var p = projects.FirstOrDefault(x => x.Id == dto.OrderedIds[i]);
			if (p != null) { p.Position = i; p.UpdatedAt = DateTime.UtcNow; }
		}
		await Db.SaveChangesAsync(ct);
	}

	public async Task<AllocationDto> AllocateAsync(CreateAllocationDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		await CreateAllocationValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
			?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
		if (project.IsArchived)
			throw new InvalidOperationException("Cannot allocate to an archived project.");

		var existing = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.ProjectId == dto.ProjectId && a.CollaboratorId == dto.CollaboratorId, ct);

		if (existing != null)
		{
			if (dto.Note != null) { existing.Note = dto.Note; existing.UpdatedAt = DateTime.UtcNow; await Db.SaveChangesAsync(ct); }
			return AllocationMapper.ToDto(existing);
		}

		var maxPos = await Db.ProjectAllocations.Where(a => a.ProjectId == dto.ProjectId)
			.Select(a => (int?)a.Position).MaxAsync(ct) ?? -1;

		var alloc = new ProjectAllocation
		{
			Id = Guid.CreateVersion7(),
			ProjectId = dto.ProjectId,
			CollaboratorId = dto.CollaboratorId,
			Note = dto.Note,
			Position = maxPos + 1,
			AllocatedAt = DateTime.UtcNow,
			AllocatedById = currentCollaboratorId,
		};
		Db.ProjectAllocations.Add(alloc);
		await Db.SaveChangesAsync(ct);

		await Db.Entry(alloc).Reference(a => a.Collaborator).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Role).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Department).LoadAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task<AllocationDto> MoveAllocationAsync(Guid allocationId, MoveAllocationDto dto, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");

		var target = await Db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
			?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
		if (target.IsArchived)
			throw new InvalidOperationException("Cannot move to an archived project.");

		// Merge: if the collaborator already has an allocation in the target project, keep that one and drop this.
		if (dto.ProjectId != alloc.ProjectId)
		{
			var clash = await Db.ProjectAllocations
				.Include(a => a.Collaborator).ThenInclude(c => c.Role)
				.Include(a => a.Collaborator).ThenInclude(c => c.Department)
				.FirstOrDefaultAsync(a => a.ProjectId == dto.ProjectId && a.CollaboratorId == alloc.CollaboratorId, ct);
			if (clash != null)
			{
				if (alloc.Note != null && clash.Note == null) clash.Note = alloc.Note;
				alloc.IsDeleted = true;
				alloc.DeletedAt = DateTime.UtcNow;
				clash.Position = dto.Position;
				clash.UpdatedAt = DateTime.UtcNow;
				await Db.SaveChangesAsync(ct);
				return AllocationMapper.ToDto(clash);
			}
		}

		alloc.ProjectId = dto.ProjectId;
		alloc.Position = dto.Position;
		alloc.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task<AllocationDto> UpdateNoteAsync(Guid allocationId, UpdateNoteDto dto, CancellationToken ct = default)
	{
		await UpdateNoteValidator.ValidateAndThrowAsync(dto, ct);
		var alloc = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");
		alloc.Note = dto.Note;
		alloc.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task RemoveAllocationAsync(Guid allocationId, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");
		alloc.IsDeleted = true;
		alloc.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
}
