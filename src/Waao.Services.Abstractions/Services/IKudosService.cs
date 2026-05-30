using Waao.Services.Abstractions.Dtos.Kudos;

namespace Waao.Services.Abstractions.Services;

public interface IKudosService
{
	Task<KudoDto> GiveAsync(GiveKudoDto dto, Guid giverId, CancellationToken ct = default);
	Task<KudoFeedDto> GetFeedAsync(Guid? before, int limit, CancellationToken ct = default);
	Task<IReadOnlyList<KudoDto>> GetReceivedAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<KudoDto>> GetGivenAsync(Guid giverId, CancellationToken ct = default);
}
