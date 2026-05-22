using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Calendar;
using Waao.Services.Abstractions.Services;
using CalendarEntity = Waao.Domain.Models.Entities.Calendar.Calendar;

namespace Waao.Services.Services;

public sealed class CalendarService(WaaoDbContext Db) : ICalendarService
{
	public async Task<Guid> EnsurePersonalCalendarAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var existing = await Db.Calendars
			.FirstOrDefaultAsync(c => c.Scope == CalendarScope.Personal && c.OwnerId == collaboratorId, ct);

		if (existing is not null) return existing.Id;

		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var calendar = new CalendarEntity
		{
			Id = Guid.CreateVersion7(),
			Name = collaborator.FullName,
			ColorHex = "#2A6B7E",
			Scope = CalendarScope.Personal,
			OwnerId = collaboratorId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.Calendars.Add(calendar);
		await Db.SaveChangesAsync(ct);
		return calendar.Id;
	}

	public async Task<IReadOnlyList<CalendarDto>> ListVisibleCalendarsAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		// Ensure the caller has a personal calendar.
		await EnsurePersonalCalendarAsync(collaboratorId, ct);

		// Determine the caller's department.
		var collaborator = await Db.Collaborators
			.Include(c => c.Department)
			.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var deptId = collaborator.DepartmentId;

		// Build the visible set:
		// 1. Personal calendar belonging to this collaborator
		// 2. Department calendar matching the caller's department (if any)
		// 3. Company-scope calendars (all of them — there is typically one)
		var calendars = await Db.Calendars
			.Include(c => c.Department)
			.Where(c =>
				(c.Scope == CalendarScope.Personal && c.OwnerId == collaboratorId) ||
				(c.Scope == CalendarScope.Department && deptId != null && c.DepartmentId == deptId) ||
				c.Scope == CalendarScope.Company)
			.OrderBy(c => c.Scope)
			.ThenBy(c => c.Name)
			.ToListAsync(ct);

		return calendars.Select(c => new CalendarDto
		{
			Id = c.Id,
			Name = c.Name,
			ColorHex = c.ColorHex,
			Scope = c.Scope,
			OwnerId = c.OwnerId,
			DepartmentId = c.DepartmentId,
			DepartmentName = c.Department?.Name,
			CanEdit = true,
		}).ToList();
	}
}
