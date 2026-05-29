using Waao.Services.Abstractions.Dtos.FeatureRequests;

namespace Waao.Services.Abstractions.Services;

public interface IFeatureRequestService
{
	Task<IReadOnlyList<FeatureRequestDto>> ListAsync(Guid callerId, CancellationToken ct = default);
	Task<FeatureRequestDto> CreateAsync(CreateFeatureRequestDto dto, Guid submitterId, CancellationToken ct = default);
	Task<FeatureRequestDto> ToggleUpvoteAsync(Guid requestId, Guid collaboratorId, CancellationToken ct = default);
	Task<FeatureRequestDto> UpdateStatusAsync(Guid requestId, UpdateFeatureRequestStatusDto dto, Guid actorId, CancellationToken ct = default);
	Task<FeatureRequestDto> UpdateAsync(Guid requestId, UpdateFeatureRequestDto dto, Guid actorId, CancellationToken ct = default);
	Task<IReadOnlyList<FeatureRequestCommentDto>> ListCommentsAsync(Guid requestId, CancellationToken ct = default);
	Task<FeatureRequestCommentDto> AddCommentAsync(Guid requestId, CreateFeatureRequestCommentDto dto, Guid authorId, CancellationToken ct = default);
	Task DeleteCommentAsync(Guid commentId, Guid actorId, CancellationToken ct = default);
}
