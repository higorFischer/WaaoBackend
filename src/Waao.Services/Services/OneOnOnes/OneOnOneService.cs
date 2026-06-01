using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.OneOnOnes;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.OneOnOnes;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.OneOnOnes;

public sealed class OneOnOneService(WaaoDbContext Db, ILogger<OneOnOneService> Logger) : IOneOnOneService
{
	public async Task<IReadOnlyList<OneOnOneDto>> ListMineAsync(Guid callerId, CancellationToken ct = default)
	{
		var rows = await Query()
			.Where(o => o.ManagerId == callerId || o.ReportId == callerId)
			.OrderByDescending(o => o.ScheduledDate)
			.ToListAsync(ct);
		return rows.Select(ToDto).ToList();
	}

	public async Task<OneOnOneDto> GetByIdAsync(Guid id, Guid callerId, CancellationToken ct = default)
	{
		var row = await Query().FirstOrDefaultAsync(o => o.Id == id, ct)
			?? throw new KeyNotFoundException($"OneOnOne {id} not found.");
		EnsureParticipant(row, callerId);
		return ToDto(row);
	}

	public async Task<OneOnOneDto> CreateAsync(CreateOneOnOneDto dto, Guid managerId, CancellationToken ct = default)
	{
		if (dto.ReportId == managerId)
			throw new InvalidOperationException("Manager and report must be different.");

		var manager = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == managerId, ct)
			?? throw new KeyNotFoundException($"Collaborator {managerId} not found.");
		var report = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.ReportId, ct)
			?? throw new KeyNotFoundException($"Collaborator {dto.ReportId} not found.");

		var entity = new OneOnOne
		{
			Id = Guid.CreateVersion7(),
			ManagerId = managerId,
			ManagerName = manager.FullName,
			ReportId = dto.ReportId,
			ReportName = report.FullName,
			ScheduledDate = dto.ScheduledDate,
			Status = OneOnOneStatus.Scheduled,
			Agenda = string.IsNullOrWhiteSpace(dto.Agenda) ? null : dto.Agenda.Trim(),
			CreatedAt = DateTime.UtcNow,
		};

		Db.OneOnOnes.Add(entity);
		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("1:1 {Id} created between {Manager} -> {Report}.", entity.Id, managerId, dto.ReportId);
		return ToDto(await ReloadAsync(entity.Id, ct));
	}

	public async Task<OneOnOneDto> UpdateAsync(Guid id, UpdateOneOnOneDto dto, Guid callerId, CancellationToken ct = default)
	{
		var row = await Db.OneOnOnes.Include(o => o.ActionItems).FirstOrDefaultAsync(o => o.Id == id, ct)
			?? throw new KeyNotFoundException($"OneOnOne {id} not found.");
		EnsureParticipant(row, callerId);

		row.ScheduledDate = dto.ScheduledDate;
		row.Agenda = string.IsNullOrWhiteSpace(dto.Agenda) ? null : dto.Agenda.Trim();
		row.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
		if (row.Status != dto.Status)
		{
			row.Status = dto.Status;
			row.CompletedAt = dto.Status == OneOnOneStatus.Completed ? DateTime.UtcNow : null;
		}
		row.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return ToDto(await ReloadAsync(id, ct));
	}

	public async Task DeleteAsync(Guid id, Guid callerId, CancellationToken ct = default)
	{
		var row = await Db.OneOnOnes.FirstOrDefaultAsync(o => o.Id == id, ct)
			?? throw new KeyNotFoundException($"OneOnOne {id} not found.");
		EnsureParticipant(row, callerId);
		row.IsDeleted = true;
		row.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<OneOnOneDto> AddActionItemAsync(Guid oneOnOneId, CreateActionItemDto dto, Guid callerId, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Text)) throw new InvalidOperationException("Text is required.");

		var row = await Db.OneOnOnes.Include(o => o.ActionItems).FirstOrDefaultAsync(o => o.Id == oneOnOneId, ct)
			?? throw new KeyNotFoundException($"OneOnOne {oneOnOneId} not found.");
		EnsureParticipant(row, callerId);

		string? assignedName = null;
		if (dto.AssignedToId.HasValue)
		{
			var assignee = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.AssignedToId.Value, ct);
			assignedName = assignee?.FullName;
		}

		var nextPos = row.ActionItems.Count == 0 ? 0 : row.ActionItems.Max(i => i.Position) + 1;
		row.ActionItems.Add(new OneOnOneActionItem
		{
			Id = Guid.CreateVersion7(),
			OneOnOneId = oneOnOneId,
			Text = dto.Text.Trim(),
			DueDate = dto.DueDate,
			AssignedToId = dto.AssignedToId,
			AssignedToName = assignedName,
			Position = nextPos,
			CreatedAt = DateTime.UtcNow,
		});
		row.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToDto(await ReloadAsync(oneOnOneId, ct));
	}

	public async Task<OneOnOneDto> ToggleActionItemAsync(Guid oneOnOneId, Guid itemId, Guid callerId, CancellationToken ct = default)
	{
		var row = await Db.OneOnOnes.Include(o => o.ActionItems).FirstOrDefaultAsync(o => o.Id == oneOnOneId, ct)
			?? throw new KeyNotFoundException($"OneOnOne {oneOnOneId} not found.");
		EnsureParticipant(row, callerId);

		var item = row.ActionItems.FirstOrDefault(i => i.Id == itemId)
			?? throw new KeyNotFoundException($"Action item {itemId} not found.");
		item.IsDone = !item.IsDone;
		item.DoneAt = item.IsDone ? DateTime.UtcNow : null;
		item.UpdatedAt = DateTime.UtcNow;
		row.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToDto(await ReloadAsync(oneOnOneId, ct));
	}

	public async Task<OneOnOneDto> RemoveActionItemAsync(Guid oneOnOneId, Guid itemId, Guid callerId, CancellationToken ct = default)
	{
		var row = await Db.OneOnOnes.Include(o => o.ActionItems).FirstOrDefaultAsync(o => o.Id == oneOnOneId, ct)
			?? throw new KeyNotFoundException($"OneOnOne {oneOnOneId} not found.");
		EnsureParticipant(row, callerId);
		var item = row.ActionItems.FirstOrDefault(i => i.Id == itemId);
		if (item is not null)
		{
			item.IsDeleted = true;
			item.DeletedAt = DateTime.UtcNow;
			row.UpdatedAt = DateTime.UtcNow;
			await Db.SaveChangesAsync(ct);
		}
		return ToDto(await ReloadAsync(oneOnOneId, ct));
	}

	public async Task<IReadOnlyList<OneOnOneDto>> ListForCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var rows = await Query()
			.Where(o => o.ManagerId == collaboratorId || o.ReportId == collaboratorId)
			.OrderByDescending(o => o.ScheduledDate)
			.ToListAsync(ct);
		return rows.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<OneOnOneActionItemDto>> ListMyOpenActionItemsAsync(Guid callerId, CancellationToken ct = default)
	{
		var items = await Db.Set<OneOnOneActionItem>()
			.AsNoTracking()
			.Where(i => i.AssignedToId == callerId && !i.IsDone)
			.OrderBy(i => i.DueDate ?? DateOnly.MaxValue)
			.ThenBy(i => i.Position)
			.ToListAsync(ct);

		return items.Select(MapItem).ToList();
	}

	private IQueryable<OneOnOne> Query()
		=> Db.OneOnOnes.AsNoTracking().Include(o => o.ActionItems.OrderBy(i => i.Position));

	private async Task<OneOnOne> ReloadAsync(Guid id, CancellationToken ct)
		=> await Query().FirstAsync(o => o.Id == id, ct);

	private static void EnsureParticipant(OneOnOne row, Guid callerId)
	{
		if (row.ManagerId != callerId && row.ReportId != callerId)
			throw new UnauthorizedAccessException("Only the manager or the report can access this 1:1.");
	}

	private static OneOnOneDto ToDto(OneOnOne o) => new()
	{
		Id = o.Id,
		ManagerId = o.ManagerId,
		ManagerName = o.ManagerName,
		ReportId = o.ReportId,
		ReportName = o.ReportName,
		ScheduledDate = o.ScheduledDate,
		Status = o.Status,
		Agenda = o.Agenda,
		Notes = o.Notes,
		CompletedAtUtc = o.CompletedAt,
		CreatedAtUtc = o.CreatedAt,
		UpdatedAtUtc = o.UpdatedAt,
		ActionItems = o.ActionItems.OrderBy(i => i.Position).Select(MapItem).ToList(),
	};

	private static OneOnOneActionItemDto MapItem(OneOnOneActionItem i) => new()
	{
		Id = i.Id,
		Text = i.Text,
		IsDone = i.IsDone,
		DoneAtUtc = i.DoneAt,
		DueDate = i.DueDate,
		AssignedToId = i.AssignedToId,
		AssignedToName = i.AssignedToName,
		Position = i.Position,
	};
}
