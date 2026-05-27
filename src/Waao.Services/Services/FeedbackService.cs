using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Feedback;
using Waao.Services.Abstractions.Services;
using FeedbackEntity = Waao.Domain.Models.Entities.Feedback.Feedback;

namespace Waao.Services.Services;

public sealed class FeedbackService(WaaoDbContext Db) : IFeedbackService
{
	public async Task<IReadOnlyList<FeedbackDto>> ListAsync(Guid callerId, FeedbackStatus? status = null, CancellationToken ct = default)
	{
		await EnsureAdminAsync(callerId, ct);

		var query = Db.Feedback.Include(f => f.SubmittedBy).AsQueryable();
		if (status.HasValue) query = query.Where(f => f.Status == status.Value);

		var items = await query
			.OrderByDescending(f => f.CreatedAt)
			.ToListAsync(ct);

		return items.Select(Map).ToList();
	}

	public async Task<FeedbackDto> CreateAsync(CreateFeedbackDto dto, Guid submitterId, CancellationToken ct = default)
	{
		var message = (dto.Message ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(message))
			throw new ArgumentException("Message is required.");
		if (message.Length > 4000) message = message[..4000];

		var entity = new FeedbackEntity
		{
			Id = Guid.CreateVersion7(),
			Category = dto.Category,
			Message = message,
			Status = FeedbackStatus.New,
			SubmittedById = submitterId,
			CreatedAt = DateTime.UtcNow,
		};

		Db.Feedback.Add(entity);
		await Db.SaveChangesAsync(ct);

		return await GetOneAsync(entity.Id, ct);
	}

	public async Task<FeedbackDto> UpdateStatusAsync(Guid id, UpdateFeedbackStatusDto dto, Guid actorId, CancellationToken ct = default)
	{
		await EnsureAdminAsync(actorId, ct);

		var entity = await Db.Feedback.FirstOrDefaultAsync(f => f.Id == id, ct)
			?? throw new KeyNotFoundException($"Feedback {id} not found.");

		entity.Status = dto.Status;
		entity.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return await GetOneAsync(id, ct);
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task EnsureAdminAsync(Guid callerId, CancellationToken ct)
	{
		var isAdmin = await Db.Collaborators
			.AnyAsync(c => c.Id == callerId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
		if (!isAdmin) throw new UnauthorizedAccessException("Admin role required.");
	}

	private async Task<FeedbackDto> GetOneAsync(Guid id, CancellationToken ct)
	{
		var f = await Db.Feedback
			.Include(x => x.SubmittedBy)
			.FirstAsync(x => x.Id == id, ct);
		return Map(f);
	}

	private static FeedbackDto Map(FeedbackEntity f) => new()
	{
		Id = f.Id,
		Category = f.Category,
		Message = f.Message,
		Status = f.Status,
		SubmittedById = f.SubmittedById,
		SubmittedByName = f.SubmittedBy?.FullName ?? string.Empty,
		SubmittedByPhotoUrl = f.SubmittedBy?.PhotoUrl,
		CreatedAt = f.CreatedAt,
	};
}
