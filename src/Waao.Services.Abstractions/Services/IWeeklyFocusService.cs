using Waao.Services.Abstractions.Dtos.Focus;

namespace Waao.Services.Abstractions.Services;

public interface IWeeklyFocusService
{
	Task<IReadOnlyList<WeeklyFocusDto>> ListAsync(CancellationToken ct = default);
	Task<WeeklyFocusDto?> GetCurrentPublishedAsync(CancellationToken ct = default);
	Task<WeeklyFocusDto?> GetCurrentForAdminAsync(CancellationToken ct = default);
	Task<WeeklyFocusDto> GetByIdAsync(Guid id, CancellationToken ct = default);
	Task<WeeklyFocusDto> CreateAsync(CreateWeeklyFocusDto dto, Guid ownerId, CancellationToken ct = default);
	Task<WeeklyFocusDto> UpdateAsync(Guid id, UpdateWeeklyFocusDto dto, CancellationToken ct = default);
	Task<WeeklyFocusDto> SetPublishedAsync(Guid id, bool publish, CancellationToken ct = default);
	Task<WeeklyFocusDto> ToggleGoalAsync(Guid id, Guid goalId, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);
}
