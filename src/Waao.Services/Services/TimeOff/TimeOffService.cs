using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Domain.Models.Entities.TimeOff;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.TimeOff;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services.TimeOff;

public sealed class TimeOffService(
	WaaoDbContext Db,
	INotificationService NotificationService,
	IValidator<CreateTimeOffDto> CreateTimeOffValidator,
	ILogger<TimeOffService> Logger) : ITimeOffService
{
	public async Task<TimeOffRequestDto> RequestAsync(CreateTimeOffDto dto, Guid collaboratorId, CancellationToken ct = default)
	{
		await CreateTimeOffValidator.ValidateAndThrowAsync(dto, ct);

		var collaborator = await Db.Collaborators
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var request = new TimeOffRequest
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = collaboratorId,
			CollaboratorName = collaborator.FullName,
			Type = dto.Type,
			StartDate = dto.StartDate,
			EndDate = dto.EndDate,
			Reason = dto.Reason,
			Status = TimeOffStatus.Pending,
			CreatedAt = DateTime.UtcNow,
		};

		Db.TimeOffRequests.Add(request);
		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Time off request {Id} created for collaborator {CollaboratorId}.", request.Id, collaboratorId);

		// Notify all HR + Admin collaborators
		var recipientIds = await Db.Collaborators
			.AsNoTracking()
			.Where(c => (c.RoleKind == CollaboratorRoleKind.Admin || c.RoleKind == CollaboratorRoleKind.HR)
			            && c.Id != collaboratorId
			            && !c.IsDeleted)
			.Select(c => c.Id)
			.ToListAsync(ct);

		if (recipientIds.Count > 0)
		{
			var typeLabel = dto.Type.ToString();
			var title = $"{collaborator.FullName} solicitou folga";
			var body = $"{typeLabel} · {dto.StartDate:dd/MM}–{dto.EndDate:dd/MM}";
			await NotificationService.CreateManyAsync(recipientIds, NotificationKind.TimeOffRequested, title, body, "timeoff", request.Id, collaboratorId, ct);
		}

		return ToDto(request);
	}

	public async Task<IReadOnlyList<TimeOffRequestDto>> ListMineAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var requests = await Db.TimeOffRequests
			.AsNoTracking()
			.Where(r => r.CollaboratorId == collaboratorId)
			.OrderByDescending(r => r.CreatedAt)
			.ToListAsync(ct);

		return requests.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<TimeOffRequestDto>> ListPendingAsync(CancellationToken ct = default)
	{
		var requests = await Db.TimeOffRequests
			.AsNoTracking()
			.Where(r => r.Status == TimeOffStatus.Pending)
			.OrderBy(r => r.CreatedAt)
			.ToListAsync(ct);

		return requests.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<TimeOffAbsenceDto>> GetAbsencesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
	{
		var requests = await Db.TimeOffRequests
			.AsNoTracking()
			.Where(r => r.Status == TimeOffStatus.Approved
			            && r.StartDate <= to
			            && r.EndDate >= from)
			.OrderBy(r => r.StartDate)
			.ToListAsync(ct);

		return requests.Select(r => new TimeOffAbsenceDto
		{
			CollaboratorId = r.CollaboratorId,
			CollaboratorName = r.CollaboratorName,
			Type = r.Type,
			StartDate = r.StartDate,
			EndDate = r.EndDate,
		}).ToList();
	}

	public async Task<TimeOffRequestDto> ReviewAsync(Guid id, bool approve, ReviewTimeOffDto dto, Guid reviewerId, CancellationToken ct = default)
	{
		var request = await Db.TimeOffRequests
			.FirstOrDefaultAsync(r => r.Id == id, ct)
			?? throw new KeyNotFoundException($"TimeOffRequest {id} not found.");

		if (request.Status != TimeOffStatus.Pending)
			throw new InvalidOperationException($"Only Pending requests can be reviewed. Current status: {request.Status}.");

		var reviewer = await Db.Collaborators
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == reviewerId, ct);

		request.Status = approve ? TimeOffStatus.Approved : TimeOffStatus.Rejected;
		request.ReviewedById = reviewerId;
		request.ReviewerName = reviewer?.FullName;
		request.ReviewedAt = DateTime.UtcNow;
		request.ReviewNote = dto.Note;
		request.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Time off request {Id} {Action} by {ReviewerId}.", id, approve ? "approved" : "rejected", reviewerId);

		// Notify requester
		var kind = approve ? NotificationKind.TimeOffApproved : NotificationKind.TimeOffRejected;
		var statusWord = approve ? "aprovada" : "recusada";
		var title = $"Folga {statusWord}";
		var typeLabel = request.Type.ToString();
		var body = $"{typeLabel} {request.StartDate:dd/MM}–{request.EndDate:dd/MM}";
		await NotificationService.CreateAsync(request.CollaboratorId, kind, title, body, "timeoff", id, reviewerId, ct);

		return ToDto(request);
	}

	public async Task CancelAsync(Guid id, Guid collaboratorId, CancellationToken ct = default)
	{
		var request = await Db.TimeOffRequests
			.FirstOrDefaultAsync(r => r.Id == id, ct)
			?? throw new KeyNotFoundException($"TimeOffRequest {id} not found.");

		if (request.CollaboratorId != collaboratorId)
			throw new UnauthorizedAccessException("You can only cancel your own time off requests.");

		if (request.Status != TimeOffStatus.Pending && request.Status != TimeOffStatus.Approved)
			throw new InvalidOperationException($"Cannot cancel a request with status {request.Status}.");

		request.Status = TimeOffStatus.Cancelled;
		request.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);

		Logger.LogInformation("Time off request {Id} cancelled by collaborator {CollaboratorId}.", id, collaboratorId);
	}

	private const int AnnualEntitledDays = 30; // CLT default; could be per-collaborator later.

	public async Task<TimeOffBalanceDto> GetBalanceAsync(Guid collaboratorId, int year, CancellationToken ct = default)
	{
		var yearStart = new DateOnly(year, 1, 1);
		var yearEnd = new DateOnly(year, 12, 31);

		var rows = await Db.TimeOffRequests.AsNoTracking()
			.Where(r => r.CollaboratorId == collaboratorId
			            && r.Type == TimeOffType.Vacation
			            && r.StartDate <= yearEnd
			            && r.EndDate >= yearStart
			            && (r.Status == TimeOffStatus.Approved || r.Status == TimeOffStatus.Pending))
			.ToListAsync(ct);

		int taken = 0, pending = 0;
		foreach (var r in rows)
		{
			var clampedStart = r.StartDate < yearStart ? yearStart : r.StartDate;
			var clampedEnd = r.EndDate > yearEnd ? yearEnd : r.EndDate;
			var days = clampedEnd.DayNumber - clampedStart.DayNumber + 1;
			if (r.Status == TimeOffStatus.Approved) taken += days;
			else if (r.Status == TimeOffStatus.Pending) pending += days;
		}

		return new TimeOffBalanceDto
		{
			EntitledDays = AnnualEntitledDays,
			TakenDays = taken,
			PendingDays = pending,
			RemainingDays = Math.Max(0, AnnualEntitledDays - taken),
			Year = year,
		};
	}

	public async Task<IReadOnlyList<TimeOffOverlapDto>> GetOverlapsAsync(DateOnly from, DateOnly to, Guid? excludeCollaboratorId, CancellationToken ct = default)
	{
		var query = Db.TimeOffRequests.AsNoTracking()
			.Include(r => r.Collaborator).ThenInclude(c => c.Department)
			.Where(r => (r.Status == TimeOffStatus.Approved || r.Status == TimeOffStatus.Pending)
			            && r.StartDate <= to
			            && r.EndDate >= from);

		if (excludeCollaboratorId.HasValue)
			query = query.Where(r => r.CollaboratorId != excludeCollaboratorId.Value);

		var rows = await query.OrderBy(r => r.StartDate).ToListAsync(ct);

		return rows.Select(r => new TimeOffOverlapDto
		{
			CollaboratorId = r.CollaboratorId,
			CollaboratorName = r.CollaboratorName,
			Type = r.Type,
			StartDate = r.StartDate,
			EndDate = r.EndDate,
			Status = r.Status,
			DepartmentName = r.Collaborator?.Department?.Name,
		}).ToList();
	}

	private static TimeOffRequestDto ToDto(TimeOffRequest r) => new()
	{
		Id = r.Id,
		CollaboratorId = r.CollaboratorId,
		CollaboratorName = r.CollaboratorName,
		Type = r.Type,
		StartDate = r.StartDate,
		EndDate = r.EndDate,
		Reason = r.Reason,
		Status = r.Status,
		ReviewedById = r.ReviewedById,
		ReviewerName = r.ReviewerName,
		ReviewedAt = r.ReviewedAt,
		ReviewNote = r.ReviewNote,
		CreatedAtUtc = r.CreatedAt,
		Days = r.EndDate.DayNumber - r.StartDate.DayNumber + 1,
	};
}
