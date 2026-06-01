using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Domain.Models.Enums;
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
	IValidator<UpdateNoteDto> UpdateNoteValidator,
	IValidator<CreateConnectionDto> CreateConnectionValidator,
	IValidator<UpdatePositionDto> UpdatePositionValidator,
	IValidator<SetParentDto> SetParentValidator) : IAllocationService
{
	public async Task<AllocationBoardDto> GetBoardAsync(CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.AsNoTracking()
			.Where(p => !p.IsArchived)
			.OrderBy(p => p.Position).ThenBy(p => p.Title)
			.Include(p => p.Department)
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

		var connections = await Db.ProjectConnections
			.AsNoTracking()
			.Where(c => !c.SourceProject.IsArchived && !c.TargetProject.IsArchived)
			.ToListAsync(ct);

		return new AllocationBoardDto
		{
			Projects = projects.Select(AllocationMapper.ToDto).ToList(),
			Collaborators = collaborators.Select(AllocationMapper.ToChip).ToList(),
			Connections = connections.Select(AllocationMapper.ToDto).ToList(),
		};
	}

	public async Task<IReadOnlyList<ProjectWithAllocationsDto>> GetByCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.AsNoTracking()
			.Where(p => !p.IsArchived && p.Allocations.Any(a => a.CollaboratorId == collaboratorId))
			.OrderBy(p => p.Position)
			.Include(p => p.Department)
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
			DepartmentId = dto.DepartmentId,
			Position = maxPos + 1,
			PositionX = (maxPos + 1) % 4 * 280,
			PositionY = (maxPos + 1) / 4 * 200,
		};
		Db.Projects.Add(project);
		await Db.SaveChangesAsync(ct);

		var reloaded = await Db.Projects
			.Include(p => p.Department)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstAsync(p => p.Id == project.Id, ct);
		return AllocationMapper.ToDto(reloaded);
	}

	public async Task<ProjectWithAllocationsDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto, CancellationToken ct = default)
	{
		await UpdateProjectValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects
			.Include(p => p.Department)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		project.Title = dto.Title;
		project.Description = dto.Description;
		project.ColorHex = dto.ColorHex;
		project.DepartmentId = dto.DepartmentId;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		// Refresh department nav after DepartmentId change
		await Db.Entry(project).Reference(p => p.Department).LoadAsync(ct);
		return AllocationMapper.ToDto(project);
	}

	public async Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
	{
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		// Re-home direct children to this project's parent (or top level) so deleting a
		// parent never leaves children pointing at an archived box.
		var children = await Db.Projects.Where(p => p.ParentProjectId == projectId).ToListAsync(ct);
		foreach (var child in children)
		{
			child.ParentProjectId = project.ParentProjectId;
			child.UpdatedAt = DateTime.UtcNow;
		}

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
		RecordEvent(dto.CollaboratorId, project, AllocationEventType.Assigned, currentCollaboratorId);
		await Db.SaveChangesAsync(ct);

		await Db.Entry(alloc).Reference(a => a.Collaborator).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Role).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Department).LoadAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task BulkAllocateAsync(BulkAllocateDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
			?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
		if (project.IsArchived)
			throw new InvalidOperationException("Cannot allocate to an archived project.");

		var ids = dto.CollaboratorIds.Distinct().ToList();
		if (ids.Count == 0)
			return;

		var alreadyIn = (await Db.ProjectAllocations
			.Where(a => a.ProjectId == dto.ProjectId && ids.Contains(a.CollaboratorId))
			.Select(a => a.CollaboratorId)
			.ToListAsync(ct)).ToHashSet();

		var maxPos = await Db.ProjectAllocations.Where(a => a.ProjectId == dto.ProjectId)
			.Select(a => (int?)a.Position).MaxAsync(ct) ?? -1;

		foreach (var collaboratorId in ids)
		{
			if (alreadyIn.Contains(collaboratorId))
				continue;
			maxPos++;
			Db.ProjectAllocations.Add(new ProjectAllocation
			{
				Id = Guid.CreateVersion7(),
				ProjectId = dto.ProjectId,
				CollaboratorId = collaboratorId,
				Position = maxPos,
				AllocatedAt = DateTime.UtcNow,
				AllocatedById = currentCollaboratorId,
			});
			RecordEvent(collaboratorId, project, AllocationEventType.Assigned, currentCollaboratorId);
		}

		await Db.SaveChangesAsync(ct);
	}

	public async Task<AllocationDto> MoveAllocationAsync(Guid allocationId, MoveAllocationDto dto, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");

		var sourceProject = await Db.Projects.FirstOrDefaultAsync(p => p.Id == alloc.ProjectId, ct);

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
				if (sourceProject != null)
					RecordEvent(alloc.CollaboratorId, sourceProject, AllocationEventType.Unassigned, null);
				await Db.SaveChangesAsync(ct);
				return AllocationMapper.ToDto(clash);
			}
		}

		if (dto.ProjectId != alloc.ProjectId && sourceProject != null)
		{
			RecordEvent(alloc.CollaboratorId, sourceProject, AllocationEventType.Unassigned, null);
			RecordEvent(alloc.CollaboratorId, target, AllocationEventType.Assigned, null);
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
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == alloc.ProjectId, ct);
		if (project != null)
			RecordEvent(alloc.CollaboratorId, project, AllocationEventType.Unassigned, null);
		alloc.IsDeleted = true;
		alloc.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task UpdateProjectPositionAsync(Guid projectId, UpdatePositionDto dto, CancellationToken ct = default)
	{
		await UpdatePositionValidator.ValidateAndThrowAsync(dto, ct);
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");
		project.PositionX = dto.X;
		project.PositionY = dto.Y;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task SetProjectParentAsync(Guid projectId, SetParentDto dto, CancellationToken ct = default)
	{
		await SetParentValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		if (dto.ParentProjectId is { } parentId)
		{
			if (parentId == projectId)
				throw new InvalidOperationException("A project cannot be its own parent.");

			var parent = await Db.Projects.FirstOrDefaultAsync(p => p.Id == parentId, ct)
				?? throw new KeyNotFoundException($"Project {parentId} not found.");
			if (parent.IsArchived)
				throw new InvalidOperationException("Cannot nest under an archived project.");

			// Cycle guard: walk up from the proposed parent; if we reach projectId, it's a cycle.
			var chain = await Db.Projects.Select(p => new { p.Id, p.ParentProjectId }).ToListAsync(ct);
			var map = chain.ToDictionary(x => x.Id, x => x.ParentProjectId);
			var cursor = (Guid?)parentId;
			var visited = new HashSet<Guid>();
			while (cursor is { } c)
			{
				if (c == projectId)
					throw new InvalidOperationException("Nesting would create a cycle.");
				if (!visited.Add(c))
					break;
				cursor = map.TryGetValue(c, out var next) ? next : null;
			}
		}

		project.ParentProjectId = dto.ParentProjectId;
		project.PositionX = dto.X;
		project.PositionY = dto.Y;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<ProjectConnectionDto> CreateConnectionAsync(CreateConnectionDto dto, CancellationToken ct = default)
	{
		await CreateConnectionValidator.ValidateAndThrowAsync(dto, ct);

		var projects = await Db.Projects.Where(p => p.Id == dto.SourceProjectId || p.Id == dto.TargetProjectId).ToListAsync(ct);
		var source = projects.FirstOrDefault(p => p.Id == dto.SourceProjectId) ?? throw new KeyNotFoundException($"Project {dto.SourceProjectId} not found.");
		var target = projects.FirstOrDefault(p => p.Id == dto.TargetProjectId) ?? throw new KeyNotFoundException($"Project {dto.TargetProjectId} not found.");
		if (source.IsArchived || target.IsArchived)
			throw new InvalidOperationException("Cannot connect an archived project.");

		var existing = await Db.ProjectConnections
			.FirstOrDefaultAsync(c => c.SourceProjectId == dto.SourceProjectId && c.TargetProjectId == dto.TargetProjectId, ct);
		if (existing != null)
		{
			if (dto.Label != null) { existing.Label = dto.Label; existing.UpdatedAt = DateTime.UtcNow; await Db.SaveChangesAsync(ct); }
			return AllocationMapper.ToDto(existing);
		}

		var conn = new ProjectConnection
		{
			Id = Guid.CreateVersion7(),
			SourceProjectId = dto.SourceProjectId,
			TargetProjectId = dto.TargetProjectId,
			Label = dto.Label,
		};
		Db.ProjectConnections.Add(conn);
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(conn);
	}

	public async Task RemoveConnectionAsync(Guid connectionId, CancellationToken ct = default)
	{
		var conn = await Db.ProjectConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
			?? throw new KeyNotFoundException($"Connection {connectionId} not found.");
		conn.IsDeleted = true;
		conn.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	private void RecordEvent(Guid collaboratorId, Project project, AllocationEventType type, Guid? actorId)
		=> Db.ProjectAllocationEvents.Add(new ProjectAllocationEvent
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = collaboratorId,
			ProjectId = project.Id,
			ProjectTitle = project.Title,
			EventType = type,
			OccurredAt = DateTime.UtcNow,
			ActorId = actorId,
		});

	public async Task<CollaboratorAllocationHistoryDto> GetCollaboratorHistoryAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var events = await Db.ProjectAllocationEvents
			.AsNoTracking()
			.Where(e => e.CollaboratorId == collaboratorId)
			.OrderBy(e => e.OccurredAt)
			.ToListAsync(ct);

		var actorIds = events.Where(e => e.ActorId.HasValue).Select(e => e.ActorId!.Value).Distinct().ToList();
		var actors = await Db.Collaborators.AsNoTracking()
			.Where(c => actorIds.Contains(c.Id))
			.ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

		// Derive per-project durations: walk events chronologically; Assigned opens a stint,
		// Unassigned closes the most recent open one. Open stint at the end = active (to now).
		var now = DateTime.UtcNow;
		var summary = events
			.GroupBy(e => new { e.ProjectId, e.ProjectTitle })
			.Select(g =>
			{
				long minutes = 0;
				int stints = 0;
				DateTime? openedAt = null;
				foreach (var e in g.OrderBy(x => x.OccurredAt))
				{
					if (e.EventType == AllocationEventType.Assigned)
					{
						openedAt ??= e.OccurredAt;
					}
					else if (e.EventType == AllocationEventType.Unassigned && openedAt is { } start)
					{
						minutes += (long)(e.OccurredAt - start).TotalMinutes;
						stints++;
						openedAt = null;
					}
				}
				var active = openedAt is { };
				if (openedAt is { } o2)
				{
					minutes += (long)(now - o2).TotalMinutes;
					stints++;
				}
				return new ProjectTimeSummaryDto
				{
					ProjectId = g.Key.ProjectId,
					ProjectTitle = g.Key.ProjectTitle,
					TotalMinutes = minutes,
					StintCount = stints,
					Active = active,
				};
			})
			.OrderByDescending(s => s.Active).ThenByDescending(s => s.TotalMinutes)
			.ToList();

		return new CollaboratorAllocationHistoryDto
		{
			CollaboratorId = collaboratorId,
			FullName = collaborator.FullName,
			Summary = summary,
			Events = events.OrderByDescending(e => e.OccurredAt)
				.Select(e => AllocationMapper.ToDto(e, e.ActorId.HasValue && actors.TryGetValue(e.ActorId.Value, out var n) ? n : null))
				.ToList(),
		};
	}

	public async Task<ProjectHistoryDto> GetProjectHistoryAsync(Guid projectId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
	{
		var events = await Db.ProjectAllocationEvents
			.AsNoTracking()
			.Where(e => e.ProjectId == projectId)
			.OrderBy(e => e.OccurredAt)
			.ToListAsync(ct);

		var project = await Db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
		var projectTitle = project?.Title
			?? events.OrderByDescending(e => e.OccurredAt).Select(e => e.ProjectTitle).FirstOrDefault()
			?? string.Empty;

		var collaboratorIds = events.Select(e => e.CollaboratorId).Distinct().ToList();
		var people = await Db.Collaborators.AsNoTracking()
			.Where(c => collaboratorIds.Contains(c.Id))
			.Include(c => c.Department)
			.ToListAsync(ct);
		var names = people.ToDictionary(c => c.Id, c => c.FullName);
		var depts = people.ToDictionary(c => c.Id, c => c.Department != null ? c.Department.Name : null);

		var actorIds = events.Where(e => e.ActorId.HasValue).Select(e => e.ActorId!.Value).Distinct().ToList();
		var actors = await Db.Collaborators.AsNoTracking()
			.Where(c => actorIds.Contains(c.Id))
			.ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

		// Derive per-collaborator durations: walk events chronologically; Assigned opens a stint,
		// Unassigned closes the most recent open one. Open stint at the end = active (to now).
		var now = DateTime.UtcNow;
		var members = events
			.GroupBy(e => e.CollaboratorId)
			.Select(g =>
			{
				long minutes = 0;
				int stints = 0;
				DateTime? openedAt = null;
				foreach (var e in g.OrderBy(x => x.OccurredAt))
				{
					if (e.EventType == AllocationEventType.Assigned)
					{
						openedAt ??= e.OccurredAt;
					}
					else if (e.EventType == AllocationEventType.Unassigned && openedAt is { } start)
					{
						minutes += (long)(e.OccurredAt - start).TotalMinutes;
						stints++;
						openedAt = null;
					}
				}
				var active = openedAt is { };
				if (openedAt is { } o2)
				{
					minutes += (long)(now - o2).TotalMinutes;
					stints++;
				}
				return new CollaboratorProjectTimeDto
				{
					CollaboratorId = g.Key,
					FullName = names.TryGetValue(g.Key, out var name) ? name : string.Empty,
					DepartmentName = depts.TryGetValue(g.Key, out var dept) ? dept : null,
					TotalMinutes = minutes,
					StintCount = stints,
					Active = active,
				};
			})
			.OrderByDescending(m => m.Active).ThenByDescending(m => m.TotalMinutes)
			.ToList();

		return new ProjectHistoryDto
		{
			ProjectId = projectId,
			ProjectTitle = projectTitle,
			TotalUsers = members.Count,
			ActiveUsers = members.Count(m => m.Active),
			Members = members,
			Events = events.OrderByDescending(e => e.OccurredAt)
				.Select(e => AllocationMapper.ToDto(e, e.ActorId.HasValue && actors.TryGetValue(e.ActorId.Value, out var n) ? n : null))
				.ToList(),
		};
	}
}
