using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.Announcements;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Announcements;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.Announcements;

public sealed class AnnouncementService(WaaoDbContext Db, ILogger<AnnouncementService> Logger) : IAnnouncementService
{
	public async Task<IReadOnlyList<AnnouncementDto>> ListAllAsync(CancellationToken ct = default)
	{
		var rows = await Query()
			.OrderByDescending(a => a.StartsAtUtc)
			.ThenBy(a => a.Position)
			.ToListAsync(ct);
		return rows.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<AnnouncementDto>> ListActiveForMeAsync(Guid callerId, CancellationToken ct = default)
	{
		var me = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == callerId, ct);
		if (me is null) return [];

		var now = DateTime.UtcNow;
		var rows = await Query()
			.Where(a => !a.IsArchived)
			.ToListAsync(ct);

		return rows
			.Where(a => IsActiveNow(a, now))
			.Where(a => MatchesAudience(a, me, callerId))
			.OrderBy(a => a.Position)
			.ThenByDescending(a => a.StartsAtUtc)
			.Select(ToDto)
			.ToList();
	}

	public async Task<AnnouncementDto> CreateAsync(CreateAnnouncementDto dto, Guid creatorId, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(dto.Title)) throw new InvalidOperationException("Title is required.");
		if (dto.EndsAtUtc <= dto.StartsAtUtc) throw new InvalidOperationException("EndsAt must be after StartsAt.");

		var creator = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == creatorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {creatorId} not found.");

		var entity = new Announcement
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title.Trim(),
			Body = Trim(dto.Body),
			ImageUrl = Trim(dto.ImageUrl),
			LogoUrl = Trim(dto.LogoUrl),
			StartsAtUtc = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc),
			EndsAtUtc = DateTime.SpecifyKind(dto.EndsAtUtc, DateTimeKind.Utc),
			RecurrenceKind = dto.RecurrenceKind,
			RecurrenceUntilUtc = dto.RecurrenceUntilUtc,
			Audience = dto.Audience,
			DepartmentId = dto.Audience == AnnouncementAudience.Department ? dto.DepartmentId : null,
			TargetRoleKind = dto.Audience == AnnouncementAudience.Role ? dto.TargetRoleKind : null,
			CountdownToUtc = dto.CountdownToUtc,
			CountdownLabel = Trim(dto.CountdownLabel),
			AccentColorHex = string.IsNullOrWhiteSpace(dto.AccentColorHex) ? "#FFB300" : dto.AccentColorHex.Trim(),
			Effect = dto.Effect,
			Position = 0,
			CreatedById = creatorId,
			CreatedByName = creator.FullName,
			CreatedAt = DateTime.UtcNow,
		};

		if (dto.Audience == AnnouncementAudience.Specific && dto.TargetCollaboratorIds.Count > 0)
		{
			foreach (var cid in dto.TargetCollaboratorIds.Distinct())
			{
				entity.Targets.Add(new AnnouncementTarget
				{
					Id = Guid.CreateVersion7(),
					AnnouncementId = entity.Id,
					CollaboratorId = cid,
					CreatedAt = DateTime.UtcNow,
				});
			}
		}

		Db.Announcements.Add(entity);
		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("Announcement {Id} created by {Creator}.", entity.Id, creatorId);
		return ToDto(await ReloadAsync(entity.Id, ct));
	}

	public async Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementDto dto, CancellationToken ct = default)
	{
		var entity = await Db.Announcements.Include(a => a.Targets).FirstOrDefaultAsync(a => a.Id == id, ct)
			?? throw new KeyNotFoundException($"Announcement {id} not found.");

		entity.Title = dto.Title.Trim();
		entity.Body = Trim(dto.Body);
		entity.ImageUrl = Trim(dto.ImageUrl);
		entity.LogoUrl = Trim(dto.LogoUrl);
		entity.StartsAtUtc = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc);
		entity.EndsAtUtc = DateTime.SpecifyKind(dto.EndsAtUtc, DateTimeKind.Utc);
		entity.RecurrenceKind = dto.RecurrenceKind;
		entity.RecurrenceUntilUtc = dto.RecurrenceUntilUtc;
		entity.Audience = dto.Audience;
		entity.DepartmentId = dto.Audience == AnnouncementAudience.Department ? dto.DepartmentId : null;
		entity.TargetRoleKind = dto.Audience == AnnouncementAudience.Role ? dto.TargetRoleKind : null;
		entity.CountdownToUtc = dto.CountdownToUtc;
		entity.CountdownLabel = Trim(dto.CountdownLabel);
		entity.AccentColorHex = string.IsNullOrWhiteSpace(dto.AccentColorHex) ? "#FFB300" : dto.AccentColorHex.Trim();
		entity.Effect = dto.Effect;
		entity.Position = dto.Position;
		entity.UpdatedAt = DateTime.UtcNow;

		// Sync targets if Specific
		entity.Targets.Clear();
		if (dto.Audience == AnnouncementAudience.Specific)
		{
			foreach (var cid in dto.TargetCollaboratorIds.Distinct())
			{
				entity.Targets.Add(new AnnouncementTarget
				{
					Id = Guid.CreateVersion7(),
					AnnouncementId = entity.Id,
					CollaboratorId = cid,
					CreatedAt = DateTime.UtcNow,
				});
			}
		}

		await Db.SaveChangesAsync(ct);
		return ToDto(await ReloadAsync(id, ct));
	}

	public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
	{
		var entity = await Db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct)
			?? throw new KeyNotFoundException($"Announcement {id} not found.");
		entity.IsArchived = true;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// ── helpers ────────────────────────────────────────────────────────────

	private IQueryable<Announcement> Query() => Db.Announcements
		.AsNoTracking()
		.Include(a => a.Department)
		.Include(a => a.Targets);

	private async Task<Announcement> ReloadAsync(Guid id, CancellationToken ct) =>
		await Query().FirstAsync(a => a.Id == id, ct);

	private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

	/// <summary>
	/// Recurrence is evaluated by 'sliding' the original (startsAt, endsAt) window
	/// onto today/this-week/this-month. For non-recurring announcements we just
	/// check the literal window.
	/// </summary>
	private static bool IsActiveNow(Announcement a, DateTime now)
	{
		if (a.RecurrenceKind == RecurrenceKind.None)
			return now >= a.StartsAtUtc && now <= a.EndsAtUtc;

		if (a.RecurrenceUntilUtc.HasValue && now > a.RecurrenceUntilUtc.Value) return false;
		if (now < a.StartsAtUtc) return false;

		var startTime = a.StartsAtUtc.TimeOfDay;
		var endTime = a.EndsAtUtc.TimeOfDay;
		var nowTime = now.TimeOfDay;

		return a.RecurrenceKind switch
		{
			RecurrenceKind.Daily => nowTime >= startTime && nowTime <= endTime,
			RecurrenceKind.Weekly => now.DayOfWeek == a.StartsAtUtc.DayOfWeek && nowTime >= startTime && nowTime <= endTime,
			RecurrenceKind.Monthly => now.Day == a.StartsAtUtc.Day && nowTime >= startTime && nowTime <= endTime,
			_ => false,
		};
	}

	private static bool MatchesAudience(Announcement a, Domain.Models.Entities.Collaborator me, Guid callerId)
	{
		return a.Audience switch
		{
			AnnouncementAudience.Everyone => true,
			AnnouncementAudience.Department => a.DepartmentId.HasValue && me.DepartmentId == a.DepartmentId,
			AnnouncementAudience.Role => a.TargetRoleKind.HasValue && me.RoleKind == a.TargetRoleKind.Value,
			AnnouncementAudience.Specific => a.Targets.Any(t => t.CollaboratorId == callerId),
			_ => false,
		};
	}

	private static AnnouncementDto ToDto(Announcement a) => new()
	{
		Id = a.Id,
		Title = a.Title,
		Body = a.Body,
		ImageUrl = a.ImageUrl,
		LogoUrl = a.LogoUrl,
		StartsAtUtc = a.StartsAtUtc,
		EndsAtUtc = a.EndsAtUtc,
		RecurrenceKind = a.RecurrenceKind,
		RecurrenceUntilUtc = a.RecurrenceUntilUtc,
		Audience = a.Audience,
		DepartmentId = a.DepartmentId,
		DepartmentName = a.Department?.Name,
		TargetRoleKind = a.TargetRoleKind,
		CountdownToUtc = a.CountdownToUtc,
		CountdownLabel = a.CountdownLabel,
		AccentColorHex = a.AccentColorHex,
		Effect = a.Effect,
		Position = a.Position,
		IsArchived = a.IsArchived,
		CreatedById = a.CreatedById,
		CreatedByName = a.CreatedByName,
		TargetCollaboratorIds = a.Targets.Select(t => t.CollaboratorId).ToList(),
	};
}
