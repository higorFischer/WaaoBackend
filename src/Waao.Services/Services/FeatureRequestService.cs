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
			.Include(r => r.Comments)
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

	public async Task<FeatureRequestDto> UpdateAsync(Guid requestId, UpdateFeatureRequestDto dto, Guid actorId, CancellationToken ct = default)
	{
		var request = await Db.FeatureRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
			?? throw new KeyNotFoundException($"Feature request {requestId} not found.");

		if (request.SubmittedById != actorId)
		{
			var isAdmin = await Db.Collaborators
				.AnyAsync(c => c.Id == actorId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
			if (!isAdmin)
				throw new UnauthorizedAccessException("Only the submitter or an admin can edit this feature request.");
		}

		var title = (dto.Title ?? string.Empty).Trim();
		var description = (dto.Description ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
			throw new ArgumentException("Title and description are required.");

		request.Title = title.Length > 160 ? title[..160] : title;
		request.Description = description.Length > 4000 ? description[..4000] : description;
		request.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		return await GetOneAsync(requestId, actorId, ct);
	}

	public async Task<IReadOnlyList<FeatureRequestCommentDto>> ListCommentsAsync(Guid requestId, CancellationToken ct = default)
	{
		var comments = await Db.FeatureRequestComments
			.AsNoTracking()
			.Include(c => c.Author)
			.Where(c => c.FeatureRequestId == requestId)
			.OrderBy(c => c.CreatedAt)
			.ToListAsync(ct);

		return comments.Select(MapComment).ToList();
	}

	public async Task<FeatureRequestCommentDto> AddCommentAsync(Guid requestId, CreateFeatureRequestCommentDto dto, Guid authorId, CancellationToken ct = default)
	{
		var exists = await Db.FeatureRequests.AnyAsync(r => r.Id == requestId, ct);
		if (!exists)
			throw new KeyNotFoundException($"Feature request {requestId} not found.");

		var body = (dto.Body ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(body))
			throw new ArgumentException("Comment body is required.");

		var comment = new FeatureRequestComment
		{
			Id = Guid.CreateVersion7(),
			FeatureRequestId = requestId,
			AuthorId = authorId,
			Body = body.Length > 2000 ? body[..2000] : body,
			CreatedAt = DateTime.UtcNow,
		};
		Db.FeatureRequestComments.Add(comment);
		await Db.SaveChangesAsync(ct);

		var saved = await Db.FeatureRequestComments
			.AsNoTracking()
			.Include(c => c.Author)
			.FirstAsync(c => c.Id == comment.Id, ct);
		return MapComment(saved);
	}

	public async Task DeleteCommentAsync(Guid commentId, Guid actorId, CancellationToken ct = default)
	{
		var comment = await Db.FeatureRequestComments.FirstOrDefaultAsync(c => c.Id == commentId, ct)
			?? throw new KeyNotFoundException($"Feature request comment {commentId} not found.");

		if (comment.AuthorId != actorId)
		{
			var isAdmin = await Db.Collaborators
				.AnyAsync(c => c.Id == actorId && c.RoleKind == CollaboratorRoleKind.Admin, ct);
			if (!isAdmin)
				throw new UnauthorizedAccessException("Only the author or an admin can delete this comment.");
		}

		comment.IsDeleted = true;
		comment.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	// =====================================================================
	// HELPERS
	// =====================================================================

	private async Task<FeatureRequestDto> GetOneAsync(Guid id, Guid callerId, CancellationToken ct)
	{
		var r = await Db.FeatureRequests
			.Include(x => x.SubmittedBy)
			.Include(x => x.Votes)
			.Include(x => x.Comments)
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
		CommentCount = r.Comments?.Count ?? 0,
	};

	private static FeatureRequestCommentDto MapComment(FeatureRequestComment c) => new()
	{
		Id = c.Id,
		Body = c.Body,
		AuthorId = c.AuthorId,
		AuthorName = c.Author?.FullName ?? string.Empty,
		AuthorPhotoUrl = c.Author?.PhotoUrl,
		CreatedAt = c.CreatedAt,
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
