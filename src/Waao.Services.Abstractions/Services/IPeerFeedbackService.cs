using Waao.Services.Abstractions.Dtos.Feedback;

namespace Waao.Services.Abstractions.Services;

public interface IPeerFeedbackService
{
	Task<PeerFeedbackDto> GiveAsync(GivePeerFeedbackDto dto, Guid giverId, CancellationToken ct = default);
	Task<IReadOnlyList<PeerFeedbackDto>> ListReceivedAsync(Guid collaboratorId, Guid callerId, bool callerIsStaff, CancellationToken ct = default);
	Task<IReadOnlyList<PeerFeedbackDto>> ListGivenAsync(Guid collaboratorId, Guid callerId, CancellationToken ct = default);
	Task<PeerFeedbackDto> AcknowledgeAsync(Guid id, Guid callerId, CancellationToken ct = default);
}
