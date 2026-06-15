using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Abstractions.Services;

public interface ICollaboratorService
{
	Task<IReadOnlyList<CollaboratorDto>> GetAllAsync(CancellationToken ct = default);
	Task<CollaboratorDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
	Task<CollaboratorDto> CreateAsync(CreateCollaboratorDto dto, CancellationToken ct = default);
	Task<CollaboratorDto> UpdateAsync(Guid id, UpdateCollaboratorDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);

	/// <summary>
	/// The caller's direct reports as lightweight summaries. When <paramref name="all"/> is true
	/// AND the caller is HR/Admin, returns every active collaborator (the team-wide overview).
	/// A non-staff caller passing <c>all=true</c> still only sees their own direct reports.
	/// </summary>
	Task<IReadOnlyList<TeamMemberSummaryDto>> GetMyTeamAsync(Guid callerId, bool all = false, CancellationToken ct = default);
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
	Task<RegisterResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
	Task ChangePasswordAsync(Guid collaboratorId, ChangePasswordDto dto, CancellationToken ct = default);
	Task<CollaboratorDto?> GetMeAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<AuthResultDto> VerifyEmailAsync(VerifyEmailDto dto, CancellationToken ct = default);
	Task ResendVerificationAsync(ResendVerificationDto dto, CancellationToken ct = default);
	Task<AuthResultDto> RefreshAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<CollaboratorDto> UpdateMyProfileAsync(Guid collaboratorId, UpdateMyProfileDto dto, CancellationToken ct = default);
	Task<CollaboratorDto> UpdateMyPhotoAsync(Guid collaboratorId, string photoUrl, CancellationToken ct = default);
	Task<CollaboratorDto> SetDesktopNotificationsEnabledAsync(Guid collaboratorId, bool enabled, CancellationToken ct = default);
}

public interface IGamificationService
{
	Task<LevelProgressDto> GetLevelProgressAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<CollaboratorBadgeDto>> GetBadgesAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<XpTransactionDto>> GetXpHistoryAsync(Guid collaboratorId, int take = 50, CancellationToken ct = default);
	Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(int take = 20, CancellationToken ct = default);
	Task<IReadOnlyList<BadgeDto>> GetAllBadgesAsync(CancellationToken ct = default);
}

public interface IOnboardingService
{
	Task<OnboardingStatusDto> GetStatusAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<OnboardingStatusDto> CompleteAsync(Guid collaboratorId, CompleteOnboardingDto dto, CancellationToken ct = default);
}
