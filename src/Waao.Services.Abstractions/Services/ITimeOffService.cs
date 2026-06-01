using Waao.Services.Abstractions.Dtos.TimeOff;

namespace Waao.Services.Abstractions.Services;

public interface ITimeOffService
{
	Task<TimeOffRequestDto> RequestAsync(CreateTimeOffDto dto, Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<TimeOffRequestDto>> ListMineAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<TimeOffRequestDto>> ListPendingAsync(CancellationToken ct = default);
	Task<IReadOnlyList<TimeOffAbsenceDto>> GetAbsencesAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
	Task<TimeOffRequestDto> ReviewAsync(Guid id, bool approve, ReviewTimeOffDto dto, Guid reviewerId, CancellationToken ct = default);
	Task CancelAsync(Guid id, Guid collaboratorId, CancellationToken ct = default);

	Task<TimeOffBalanceDto> GetBalanceAsync(Guid collaboratorId, int year, CancellationToken ct = default);
	Task<IReadOnlyList<TimeOffOverlapDto>> GetOverlapsAsync(DateOnly from, DateOnly to, Guid? excludeCollaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<TimeOffRequestDto>> ListForCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default);
}
