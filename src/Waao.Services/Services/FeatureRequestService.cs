using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities.FeatureRequests;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.FeatureRequests;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class FeatureRequestService(
	WaaoDbContext Db,
	INotificationService NotificationService) : IFeatureRequestService
{
	public async Task<IReadOnlyList<FeatureRequestDto>> ListAsync(Guid callerId, CancellationToken ct = default)
	{
		var requests = await Db.FeatureRequests
			.Include(r => r.SubmittedBy)
			.Include(r => r.Votes)
			.ToListAsync(ct);

		return requests
			.Select(r => Map(r, callerId))
			.OrderBy(d => StatusOrder(d.Status))
			.ThenByDescending(d => d.VoteCount)
			.ThenByDescending(d => d.CreatedAt)
			.ToList();
	}

	public async Task<FeatureRequestDto> CreateAsync(CreateFeatureRequestDto dto, Guid submitterId, CancellationToken ct = default)
	{
		var title = (dto.Title ?? string.Empty).Trim();
		var description = (dto.Description ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
			throw new ArgumentException("Title and description are required.");

		var entity = new FeatureRequest
		{
			Id = Guid.CreateVersion7(),
			Title = title.Length > 160 ? title[..160] : title,
			Description = description.Length > 4000 ? description[..4000] : description,
			Status = FeatureRequestStatus.New,
			SubmittedById = submitterId,
			CreatedAt = DateTime.UtcNow,
		};
		Db.FeatureRequests.Add(entity);

		// Submitter implicitly upvotes their own request.
		Db.FeatureRequestVotes.Add(new FeatureRequestVote
		{
			Id = Guid.CreateVersion7(),
			FeatureRequestId = entity.Id,
			CollaboratorId = submitterId,
			CreatedAt = DateTime.UtcNow,
		});

		await Db.SaveChangesAsync(ct);

		return await GetOneAsync(entity.Id, submitterId, ct);
	}

	public async Task<FeatureRequestDto> ToggleUpvoteAsync(Guid requestId, Guid collaboratorId, CancellationToken ct = default)
	{
		var request = await Db.FeatureRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
			?? throw new KeyNotFoundException($"Feature request {requestId} not found.");

		var existing = await Db.FeatureRequestVotes.IgnoreQueryFilters()
			.FirstOrDefaultAsync(v => v.FeatureRequestId == requestId && v.CollaboratorId == collaboratorId, ct);

		if (existing is null)
		{
			Db.FeatureRequestVotes.Add(new FeatureRequestVote
			{
				Id = Guid.CreateVersion7(),
				FeatureRequestId = requestId,
				CollaboratorId = collaboratorId,
				CreatedAt = DateTime.UtcNow,
			});
		}
		else if (existing.IsDeleted)
		{
			existing.IsDeleted = false;
			existing.DeletedAt = null;
			existing.UpdatedAt = DateTime.UtcNow;
		}
		else
		{
			existing.IsDeleted = true;
			existing.DeletedAt = DateTime.UtcNow;
		}

		await Db.SaveChangesAsync(ct);
		return await GetOneAsync(requestId, collaboratorId, ct);
	}

	public async Task<FeatureRequestDto> UpdateStatusAsync(Guid requestId, UpdateFeatureRequestStatusDto dto, Guid actorId, CancellationToken ct = default)
	{
		var isAdmin = await Db.Collaborators
			.AnyAsync(c => c.Id == actorId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
		if (!isAdmin)
			throw new UnauthorizedAccessException("Only admins can update feature request status.");

		var request = await Db.FeatureRequests
			.Include(r => r.SubmittedBy)
			.FirstOrDefaultAsync(r => r.Id == requestId, ct)
			?? throw new KeyNotFoundException($"Feature request {requestId} not found.");

		var prevStatus = request.Status;
		request.Status = dto.Status;
		request.AdminResponse = string.IsNullOrWhiteSpace(dto.AdminResponse) ? null : dto.AdminResponse.Trim();
		request.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		// Notify the submitter only when the status actually changed.
		if (prevStatus != dto.Status && request.SubmittedById != actorId)
		{
			await NotificationService.CreateAsync(
				request.SubmittedById,
				NotificationKind.FeatureRequestStatus,
				$"Feature request \"{Truncate(request.Title, 60)}\" → {dto.Status}",
				request.AdminResponse ?? $"Status changed to {dto.Status}.",
				"feature-request",
				request.Id,
				actorId,
				ct);
		}

		return await GetOneAsync(requestId, actorId, ct);
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task<FeatureRequestDto> GetOneAsync(Guid id, Guid callerId, CancellationToken ct)
	{
		var r = await Db.FeatureRequests
			.Include(x => x.SubmittedBy)
			.Include(x => x.Votes)
			.FirstAsync(x => x.Id == id, ct);
		return Map(r, callerId);
	}

	private static FeatureRequestDto Map(FeatureRequest r, Guid callerId) => new()
	{
		Id = r.Id,
		Title = r.Title,
		Description = r.Description,
		Status = r.Status,
		AdminResponse = r.AdminResponse,
		SubmittedById = r.SubmittedById,
		SubmittedByName = r.SubmittedBy?.FullName ?? string.Empty,
		SubmittedByPhotoUrl = r.SubmittedBy?.PhotoUrl,
		CreatedAt = r.CreatedAt,
		VoteCount = r.Votes?.Count ?? 0,
		HasUpvoted = r.Votes?.Any(v => v.CollaboratorId == callerId) ?? false,
	};

	private static int StatusOrder(FeatureRequestStatus s) => s switch
	{
		FeatureRequestStatus.InProgress => 0,
		FeatureRequestStatus.Planned => 1,
		FeatureRequestStatus.New => 2,
		FeatureRequestStatus.Done => 3,
		FeatureRequestStatus.Declined => 4,
		_ => 5,
	};

	private static string Truncate(string s, int max) =>
		string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
