using Waao.Services.Abstractions.Dtos.FeatureRequests;

namespace Waao.Services.Abstractions.Services;

public interface IFeatureRequestService
{
	Task<IReadOnlyList<FeatureRequestDto>> ListAsync(Guid callerId, CancellationToken ct = default);
	Task<FeatureRequestDto> CreateAsync(CreateFeatureRequestDto dto, Guid submitterId, CancellationToken ct = default);
	Task<FeatureRequestDto> ToggleUpvoteAsync(Guid requestId, Guid collaboratorId, CancellationToken ct = default);
	Task<FeatureRequestDto> UpdateStatusAsync(Guid requestId, UpdateFeatureRequestStatusDto dto, Guid actorId, CancellationToken ct = default);
}
