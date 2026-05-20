using Waao.Services.Abstractions.Dtos.Challenges;

namespace Waao.Services.Abstractions.Services;

public interface IChallengeService
{
	Task<IReadOnlyList<ChallengeDto>> ListAsync(bool isAdminOrHr, CancellationToken ct = default);
	Task<ChallengeDto> GetByIdAsync(Guid id, bool isAdminOrHr, CancellationToken ct = default);
	Task<PublicChallengeDto> GetPublicByIdAsync(Guid id, CancellationToken ct = default);
	Task<ChallengeDto> CreateAsync(CreateChallengeDto dto, Guid authorId, CancellationToken ct = default);
	Task<ChallengeDto> UpdateAsync(Guid id, UpdateChallengeDto dto, CancellationToken ct = default);
	Task DeleteAsync(Guid id, CancellationToken ct = default);
	Task<ChallengeDto> PublishAsync(Guid id, bool isPublished, CancellationToken ct = default);
	Task<ChallengeQuestionDto> AddQuestionAsync(Guid challengeId, CreateChallengeQuestionDto dto, CancellationToken ct = default);
	Task<ChallengeQuestionDto> UpdateQuestionAsync(Guid challengeId, Guid questionId, UpdateChallengeQuestionDto dto, CancellationToken ct = default);
	Task DeleteQuestionAsync(Guid challengeId, Guid questionId, CancellationToken ct = default);
	Task ReorderQuestionsAsync(Guid challengeId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}

public interface IChallengeAttemptService
{
	Task<PublicChallengeDto> StartAsync(Guid challengeId, Guid collaboratorId, CancellationToken ct = default);
	Task<ChallengeAttemptResultDto> SubmitAsync(Guid attemptId, SubmitChallengeAttemptDto dto, Guid collaboratorId, CancellationToken ct = default);
	Task<IReadOnlyList<ChallengeAttemptDto>> ListPendingForReviewAsync(CancellationToken ct = default);
	Task<ChallengeAttemptDto> GrantXpForAttemptAsync(Guid attemptId, GrantChallengeXpDto dto, Guid adminId, CancellationToken ct = default);
	Task<IReadOnlyList<ChallengeAttemptDto>> ListMyAttemptsAsync(Guid collaboratorId, CancellationToken ct = default);
}
