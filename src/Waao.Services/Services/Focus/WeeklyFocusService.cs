using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.Focus;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Focus;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.Focus;

public sealed class WeeklyFocusService(
	WaaoDbContext Db,
	ILogger<WeeklyFocusService> Logger) : IWeeklyFocusService
{
	public async Task<IReadOnlyList<WeeklyFocusDto>> ListAsync(CancellationToken ct = default)
	{
		var list = await Query()
			.OrderByDescending(f => f.IsoYear)
			.ThenByDescending(f => f.IsoWeek)
			.ToListAsync(ct);

		return await HydrateManyAsync(list, ct);
	}

	public async Task<WeeklyFocusDto?> GetCurrentPublishedAsync(CancellationToken ct = default)
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var focus = await Query()
			.Where(f => f.IsPublished && f.StartDate <= today && f.EndDate >= today)
			.OrderByDescending(f => f.PublishedAt)
			.FirstOrDefaultAsync(ct);

		return focus is null ? null : await HydrateAsync(focus, ct);
	}

	public async Task<WeeklyFocusDto?> GetCurrentForAdminAsync(CancellationToken ct = default)
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		var focus = await Query()
			.Where(f => f.StartDate <= today && f.EndDate >= today)
			.OrderByDescending(f => f.UpdatedAt ?? f.CreatedAt)
			.FirstOrDefaultAsync(ct);

		if (focus is not null) return await HydrateAsync(focus, ct);

		var latest = await Query()
			.OrderByDescending(f => f.IsoYear)
			.ThenByDescending(f => f.IsoWeek)
			.FirstOrDefaultAsync(ct);

		return latest is null ? null : await HydrateAsync(latest, ct);
	}

	public async Task<WeeklyFocusDto> GetByIdAsync(Guid id, CancellationToken ct = default)
	{
		var focus = await Query().FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"WeeklyFocus {id} not found.");

		return await HydrateAsync(focus, ct);
	}

	public async Task<WeeklyFocusDto> CreateAsync(CreateWeeklyFocusDto dto, Guid ownerId, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Title))
			throw new InvalidOperationException("Title is required.");
		if (dto.IsoWeek is < 1 or > 53)
			throw new InvalidOperationException("IsoWeek must be between 1 and 53.");

		var owner = await Db.Collaborators.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == ownerId, ct)
			?? throw new KeyNotFoundException($"Collaborator {ownerId} not found.");

		var (start, end) = IsoWeekRange(dto.IsoYear, dto.IsoWeek);
		var now = DateTime.UtcNow;

		var focus = new WeeklyFocus
		{
			Id = Guid.CreateVersion7(),
			IsoYear = dto.IsoYear,
			IsoWeek = dto.IsoWeek,
			StartDate = start,
			EndDate = end,
			Title = dto.Title.Trim(),
			Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
			IsPublished = dto.Publish,
			PublishedAt = dto.Publish ? now : null,
			OwnerId = ownerId,
			OwnerName = owner.FullName,
			CreatedAt = now,
		};

		var position = 0;
		foreach (var raw in dto.Goals)
		{
			var text = (raw ?? string.Empty).Trim();
			if (text.Length == 0) continue;
			focus.Goals.Add(new WeeklyFocusGoal
			{
				Id = Guid.CreateVersion7(),
				WeeklyFocusId = focus.Id,
				Text = text,
				Position = position++,
				CreatedAt = now,
			});
		}

		await AttachProjectsAsync(focus, dto.ProjectIds, now, ct);

		Db.WeeklyFocuses.Add(focus);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("WeeklyFocus {Id} created for {Year}-W{Week} by {OwnerId} (published={Publish}).",
			focus.Id, focus.IsoYear, focus.IsoWeek, ownerId, focus.IsPublished);

		return await HydrateAsync(await ReloadAsync(focus.Id, ct), ct);
	}

	public async Task<WeeklyFocusDto> UpdateAsync(Guid id, UpdateWeeklyFocusDto dto, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Title))
			throw new InvalidOperationException("Title is required.");

		var focus = await Db.WeeklyFocuses
			.Include(f => f.Goals)
			.Include(f => f.Projects)
			.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"WeeklyFocus {id} not found.");

		var (start, end) = IsoWeekRange(dto.IsoYear, dto.IsoWeek);
		var now = DateTime.UtcNow;

		focus.IsoYear = dto.IsoYear;
		focus.IsoWeek = dto.IsoWeek;
		focus.StartDate = start;
		focus.EndDate = end;
		focus.Title = dto.Title.Trim();
		focus.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
		focus.UpdatedAt = now;

		SyncGoals(focus, dto.Goals, now);
		await SyncProjectsAsync(focus, dto.ProjectIds, now, ct);

		await Db.SaveChangesAsync(ct);
		return await HydrateAsync(await ReloadAsync(id, ct), ct);
	}

	public async Task<WeeklyFocusDto> SetPublishedAsync(Guid id, bool publish, CancellationToken ct = default)
	{
		var focus = await Db.WeeklyFocuses.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"WeeklyFocus {id} not found.");

		focus.IsPublished = publish;
		focus.PublishedAt = publish ? DateTime.UtcNow : null;
		focus.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("WeeklyFocus {Id} {Action}.", id, publish ? "published" : "unpublished");

		return await HydrateAsync(await ReloadAsync(id, ct), ct);
	}

	public async Task<WeeklyFocusDto> ToggleGoalAsync(Guid id, Guid goalId, CancellationToken ct = default)
	{
		var goal = await Db.Set<WeeklyFocusGoal>()
			.FirstOrDefaultAsync(g => g.Id == goalId && g.WeeklyFocusId == id, ct)
			?? throw new KeyNotFoundException($"WeeklyFocusGoal {goalId} not found.");

		goal.IsDone = !goal.IsDone;
		goal.DoneAt = goal.IsDone ? DateTime.UtcNow : null;
		goal.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return await HydrateAsync(await ReloadAsync(id, ct), ct);
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var focus = await Db.WeeklyFocuses.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"WeeklyFocus {id} not found.");

		focus.IsDeleted = true;
		focus.DeletedAt = DateTime.UtcNow;
		focus.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("WeeklyFocus {Id} soft-deleted.", id);
	}

	// ── helpers ─────────────────────────────────────────────────────────────

	private IQueryable<WeeklyFocus> Query()
		=> Db.WeeklyFocuses
			.AsNoTracking()
			.Include(f => f.Goals.OrderBy(g => g.Position))
			.Include(f => f.Projects.OrderBy(p => p.Position));

	private async Task<WeeklyFocus> ReloadAsync(Guid id, CancellationToken ct)
		=> await Query().FirstAsync(f => f.Id == id, ct);

	private void SyncGoals(WeeklyFocus focus, IReadOnlyList<WeeklyFocusGoalInputDto> inputs, DateTime now)
	{
		var inputIds = inputs.Where(g => g.Id.HasValue).Select(g => g.Id!.Value).ToHashSet();

		foreach (var existing in focus.Goals.ToList())
		{
			if (!inputIds.Contains(existing.Id))
				focus.Goals.Remove(existing);
		}

		var position = 0;
		foreach (var input in inputs)
		{
			var text = (input.Text ?? string.Empty).Trim();
			if (text.Length == 0) continue;

			var existing = input.Id.HasValue
				? focus.Goals.FirstOrDefault(g => g.Id == input.Id.Value)
				: null;

			if (existing is null)
			{
				focus.Goals.Add(new WeeklyFocusGoal
				{
					Id = Guid.CreateVersion7(),
					WeeklyFocusId = focus.Id,
					Text = text,
					IsDone = input.IsDone,
					DoneAt = input.IsDone ? now : null,
					Position = position++,
					CreatedAt = now,
				});
			}
			else
			{
				existing.Text = text;
				if (existing.IsDone != input.IsDone)
				{
					existing.IsDone = input.IsDone;
					existing.DoneAt = input.IsDone ? now : null;
				}
				existing.Position = position++;
				existing.UpdatedAt = now;
			}
		}
	}

	private async Task AttachProjectsAsync(WeeklyFocus focus, IReadOnlyList<Guid> projectIds, DateTime now, CancellationToken ct)
	{
		if (projectIds.Count == 0) return;

		var projects = await Db.Projects.AsNoTracking()
			.Where(p => projectIds.Contains(p.Id) && !p.IsDeleted)
			.ToListAsync(ct);

		var position = 0;
		foreach (var pid in projectIds)
		{
			var project = projects.FirstOrDefault(p => p.Id == pid);
			if (project is null) continue;

			focus.Projects.Add(new WeeklyFocusProject
			{
				Id = Guid.CreateVersion7(),
				WeeklyFocusId = focus.Id,
				ProjectId = project.Id,
				ProjectTitle = project.Title,
				ProjectColorHex = project.ColorHex,
				ParentProjectId = project.ParentProjectId,
				Position = position++,
				CreatedAt = now,
			});
		}
	}

	private async Task SyncProjectsAsync(WeeklyFocus focus, IReadOnlyList<Guid> projectIds, DateTime now, CancellationToken ct)
	{
		var keep = projectIds.ToHashSet();

		foreach (var existing in focus.Projects.ToList())
		{
			if (!keep.Contains(existing.ProjectId))
				focus.Projects.Remove(existing);
		}

		var missing = projectIds
			.Where(pid => focus.Projects.All(p => p.ProjectId != pid))
			.ToList();

		var snapshot = await Db.Projects.AsNoTracking()
			.Where(p => projectIds.Contains(p.Id) && !p.IsDeleted)
			.ToDictionaryAsync(p => p.Id, ct);

		foreach (var pid in missing)
		{
			if (!snapshot.TryGetValue(pid, out var project)) continue;

			focus.Projects.Add(new WeeklyFocusProject
			{
				Id = Guid.CreateVersion7(),
				WeeklyFocusId = focus.Id,
				ProjectId = project.Id,
				ProjectTitle = project.Title,
				ProjectColorHex = project.ColorHex,
				ParentProjectId = project.ParentProjectId,
				CreatedAt = now,
			});
		}

		var position = 0;
		foreach (var pid in projectIds)
		{
			var match = focus.Projects.FirstOrDefault(p => p.ProjectId == pid);
			if (match is null) continue;

			match.Position = position++;
			match.UpdatedAt = now;

			if (snapshot.TryGetValue(pid, out var live))
			{
				match.ProjectTitle = live.Title;
				match.ProjectColorHex = live.ColorHex;
				match.ParentProjectId = live.ParentProjectId;
			}
		}
	}

	private static (DateOnly Start, DateOnly End) IsoWeekRange(int isoYear, int isoWeek)
	{
		var jan4 = new DateTime(isoYear, 1, 4, 0, 0, 0, DateTimeKind.Utc);
		var jan4Dow = (int)jan4.DayOfWeek;
		if (jan4Dow == 0) jan4Dow = 7;
		var weekOneMonday = jan4.AddDays(1 - jan4Dow);
		var start = weekOneMonday.AddDays((isoWeek - 1) * 7);
		var end = start.AddDays(6);
		return (DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
	}

	private async Task<WeeklyFocusDto> HydrateAsync(WeeklyFocus focus, CancellationToken ct)
		=> (await HydrateManyAsync(new[] { focus }, ct))[0];

	private async Task<IReadOnlyList<WeeklyFocusDto>> HydrateManyAsync(IEnumerable<WeeklyFocus> focuses, CancellationToken ct)
	{
		var list = focuses as IList<WeeklyFocus> ?? focuses.ToList();
		if (list.Count == 0) return [];

		var projectIds = list
			.SelectMany(f => f.Projects.Select(p => p.ProjectId))
			.Distinct()
			.ToList();

		Dictionary<Guid, List<WeeklyFocusAllocationDto>> allocsByProject;

		if (projectIds.Count == 0)
		{
			allocsByProject = [];
		}
		else
		{
			var rows = await Db.ProjectAllocations
				.AsNoTracking()
				.Where(a => projectIds.Contains(a.ProjectId) && !a.IsDeleted)
				.Join(Db.Collaborators.AsNoTracking().Include(c => c.Role).Where(c => !c.IsDeleted),
					a => a.CollaboratorId,
					c => c.Id,
					(a, c) => new
					{
						a.ProjectId,
						a.Position,
						a.AllocatedAt,
						CollaboratorId = c.Id,
						c.FullName,
						c.PhotoUrl,
						RoleTitle = c.Role != null ? c.Role.Title : null,
					})
				.OrderBy(x => x.Position).ThenBy(x => x.AllocatedAt)
				.ToListAsync(ct);

			allocsByProject = rows
				.GroupBy(x => x.ProjectId)
				.ToDictionary(g => g.Key, g => g.Select(x => new WeeklyFocusAllocationDto
				{
					CollaboratorId = x.CollaboratorId,
					FullName = x.FullName,
					PhotoUrl = x.PhotoUrl,
					RoleTitle = x.RoleTitle,
				}).ToList());
		}

		return list.Select(f => ToDto(f) with
		{
			Projects = f.Projects.OrderBy(p => p.Position).Select(p => new WeeklyFocusProjectDto
			{
				Id = p.Id,
				ProjectId = p.ProjectId,
				ProjectTitle = p.ProjectTitle,
				ProjectColorHex = p.ProjectColorHex,
				ParentProjectId = p.ParentProjectId,
				Position = p.Position,
				Allocations = allocsByProject.TryGetValue(p.ProjectId, out var a) ? a : [],
			}).ToList(),
		}).ToList();
	}

	private static WeeklyFocusDto ToDto(WeeklyFocus f) => new()
	{
		Id = f.Id,
		IsoYear = f.IsoYear,
		IsoWeek = f.IsoWeek,
		StartDate = f.StartDate,
		EndDate = f.EndDate,
		Title = f.Title,
		Description = f.Description,
		IsPublished = f.IsPublished,
		PublishedAt = f.PublishedAt,
		OwnerId = f.OwnerId,
		OwnerName = f.OwnerName,
		CreatedAtUtc = f.CreatedAt,
		UpdatedAtUtc = f.UpdatedAt,
		Goals = f.Goals
			.OrderBy(g => g.Position)
			.Select(g => new WeeklyFocusGoalDto
			{
				Id = g.Id,
				Text = g.Text,
				IsDone = g.IsDone,
				DoneAt = g.DoneAt,
				Position = g.Position,
			}).ToList(),
		Projects = f.Projects
			.OrderBy(p => p.Position)
			.Select(p => new WeeklyFocusProjectDto
			{
				Id = p.Id,
				ProjectId = p.ProjectId,
				ProjectTitle = p.ProjectTitle,
				ProjectColorHex = p.ProjectColorHex,
				ParentProjectId = p.ParentProjectId,
				Position = p.Position,
			}).ToList(),
	};
}
