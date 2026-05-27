using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos.Feedback;

namespace Waao.Services.Abstractions.Services;

public interface IFeedbackService
{
	/// <summary>Admin-only list. Optional status filter.</summary>
	Task<IReadOnlyList<FeedbackDto>> ListAsync(Guid callerId, FeedbackStatus? status = null, CancellationToken ct = default);

	/// <summary>Any authenticated user can submit feedback.</summary>
	Task<FeedbackDto> CreateAsync(CreateFeedbackDto dto, Guid submitterId, CancellationToken ct = default);

	/// <summary>Admin-only status transition (New → Read → Resolved / Archived).</summary>
	Task<FeedbackDto> UpdateStatusAsync(Guid id, UpdateFeedbackStatusDto dto, Guid actorId, CancellationToken ct = default);
}
