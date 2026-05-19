using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface ICollaboratorService
{
	Task<IReadOnlyList<CollaboratorDto>> GetAllAsync(CancellationToken ct = default);
	Task<CollaboratorDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
	Task<CollaboratorDto> CreateAsync(CreateCollaboratorDto dto, CancellationToken ct = default);
	Task<CollaboratorDto> UpdateAsync(Guid id, UpdateCollaboratorDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICareerEventService
{
	Task<IReadOnlyList<CareerEventDto>> GetForCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<CareerEventCreatedDto> CreateAsync(CreateCareerEventDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAuthService
{
	Task<AuthResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
	Task<AuthResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
	Task ChangePasswordAsync(Guid collaboratorId, ChangePasswordDto dto, CancellationToken ct = default);
	Task<CollaboratorDto?> GetMeAsync(Guid collaboratorId, CancellationToken ct = default);
}

public interface IGamificationService
{
	Task<LevelProgressDto> GetLevelProgressAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<CollaboratorBadgeDto>> GetBadgesAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<XpTransactionDto>> GetXpHistoryAsync(Guid collaboratorId, int take = 50, CancellationToken ct = default);
	Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int take = 20, CancellationToken ct = default);
	Task<IReadOnlyList<BadgeDto>> GetAllBadgesAsync(CancellationToken ct = default);
}
