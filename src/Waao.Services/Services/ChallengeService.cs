using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Challenges;
using Waao.Services.Abstractions.Services;
using Waao.Services.Gamification;

namespace Waao.Services.Services;

public sealed class ChallengeService(
	WaaoDbContext Db,
	IValidator<CreateChallengeDto> CreateValidator,
	IValidator<UpdateChallengeDto> UpdateValidator,
	IValidator<CreateChallengeQuestionDto> CreateQuestionValidator,
	IValidator<UpdateChallengeQuestionDto> UpdateQuestionValidator) : IChallengeService
{
	public async Task<IReadOnlyList<ChallengeDto>> ListAsync(bool isAdminOrHr, CancellationToken ct = default)
	{
		var query = Db.Challenges.Include(c => c.Questions).AsQueryable();
		if (!isAdminOrHr)
			query = query.Where(c => c.IsPublished);

		return await query
			.OrderBy(c => c.Title)
			.Select(c => ToAdminDto(c))
			.ToListAsync(ct);
	}

	public async Task<ChallengeDto> GetByIdAsync(Guid id, bool isAdminOrHr, CancellationToken ct = default)
	{
		var challenge = await Db.Challenges.Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Challenge {id} not found.");

		if (!isAdminOrHr && !challenge.IsPublished)
			throw new KeyNotFoundException($"Challenge {id} not found.");

		return ToAdminDto(challenge);
	}

	public async Task<PublicChallengeDto> GetPublicByIdAsync(Guid id, CancellationToken ct = default)
	{
		var challenge = await Db.Challenges.Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == id && c.IsPublished, ct)
			?? throw new KeyNotFoundException($"Challenge {id} not found.");

		return ToPublicDto(challenge);
	}

	public async Task<ChallengeDto> CreateAsync(CreateChallengeDto dto, Guid authorId, CancellationToken ct = default)
	{
		await CreateValidator.ValidateAndThrowAsync(dto, ct);

		var challenge = new Challenge
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title,
			Description = dto.Description,
			Category = dto.Category,
			SuggestedXp = dto.SuggestedXp,
			PassPercent = dto.PassPercent,
			IsPublished = false,
			CreatedById = authorId,
		};

		Db.Challenges.Add(challenge);
		await Db.SaveChangesAsync(ct);
		return ToAdminDto(challenge);
	}

	public async Task<ChallengeDto> UpdateAsync(Guid id, UpdateChallengeDto dto, CancellationToken ct = default)
	{
		await UpdateValidator.ValidateAndThrowAsync(dto, ct);

		var challenge = await Db.Challenges.Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Challenge {id} not found.");

		challenge.Title = dto.Title;
		challenge.Description = dto.Description;
		challenge.Category = dto.Category;
		challenge.SuggestedXp = dto.SuggestedXp;
		challenge.PassPercent = dto.PassPercent;
		challenge.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return ToAdminDto(challenge);
	}

	public async Task DeleteAsync(Guid id, CancellationToken ct = default)
	{
		var challenge = await Db.Challenges.FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Challenge {id} not found.");

		challenge.IsDeleted = true;
		challenge.DeletedAt = DateTime.UtcNow;
		challenge.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<ChallengeDto> PublishAsync(Guid id, bool isPublished, CancellationToken ct = default)
	{
		var challenge = await Db.Challenges.Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == id, ct)
			?? throw new KeyNotFoundException($"Challenge {id} not found.");

		challenge.IsPublished = isPublished;
		challenge.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToAdminDto(challenge);
	}

	public async Task<ChallengeQuestionDto> AddQuestionAsync(Guid challengeId, CreateChallengeQuestionDto dto, CancellationToken ct = default)
	{
		await CreateQuestionValidator.ValidateAndThrowAsync(dto, ct);

		_ = await Db.Challenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct)
			?? throw new KeyNotFoundException($"Challenge {challengeId} not found.");

		var question = new ChallengeQuestion
		{
			Id = Guid.CreateVersion7(),
			ChallengeId = challengeId,
			Order = dto.Order,
			Prompt = dto.Prompt,
			OptionA = dto.OptionA,
			OptionB = dto.OptionB,
			OptionC = dto.OptionC,
			OptionD = dto.OptionD,
			CorrectOption = dto.CorrectOption,
		};

		Db.ChallengeQuestions.Add(question);
		await Db.SaveChangesAsync(ct);
		return ToQuestionDto(question);
	}

	public async Task<ChallengeQuestionDto> UpdateQuestionAsync(Guid challengeId, Guid questionId, UpdateChallengeQuestionDto dto, CancellationToken ct = default)
	{
		await UpdateQuestionValidator.ValidateAndThrowAsync(dto, ct);

		var question = await Db.ChallengeQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.ChallengeId == challengeId, ct)
			?? throw new KeyNotFoundException($"Question {questionId} not found.");

		question.Order = dto.Order;
		question.Prompt = dto.Prompt;
		question.OptionA = dto.OptionA;
		question.OptionB = dto.OptionB;
		question.OptionC = dto.OptionC;
		question.OptionD = dto.OptionD;
		question.CorrectOption = dto.CorrectOption;
		question.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return ToQuestionDto(question);
	}

	public async Task DeleteQuestionAsync(Guid challengeId, Guid questionId, CancellationToken ct = default)
	{
		var question = await Db.ChallengeQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.ChallengeId == challengeId, ct)
			?? throw new KeyNotFoundException($"Question {questionId} not found.");

		question.IsDeleted = true;
		question.DeletedAt = DateTime.UtcNow;
		question.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task ReorderQuestionsAsync(Guid challengeId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
	{
		var questions = await Db.ChallengeQuestions
			.Where(q => q.ChallengeId == challengeId)
			.ToListAsync(ct);

		for (var i = 0; i < orderedIds.Count; i++)
		{
			var q = questions.FirstOrDefault(x => x.Id == orderedIds[i]);
			if (q is not null)
			{
				q.Order = i + 1;
				q.UpdatedAt = DateTime.UtcNow;
			}
		}

		await Db.SaveChangesAsync(ct);
	}

	internal static ChallengeDto ToAdminDto(Challenge c) => new()
	{
		Id = c.Id,
		Title = c.Title,
		Description = c.Description,
		Category = c.Category,
		SuggestedXp = c.SuggestedXp,
		PassPercent = c.PassPercent,
		IsPublished = c.IsPublished,
		CreatedById = c.CreatedById,
		CreatedAt = c.CreatedAt,
		Questions = c.Questions.OrderBy(q => q.Order).Select(ToQuestionDto).ToList(),
	};

	internal static PublicChallengeDto ToPublicDto(Challenge c) => new()
	{
		Id = c.Id,
		Title = c.Title,
		Description = c.Description,
		Category = c.Category,
		SuggestedXp = c.SuggestedXp,
		PassPercent = c.PassPercent,
		IsPublished = c.IsPublished,
		CreatedAt = c.CreatedAt,
		Questions = c.Questions.OrderBy(q => q.Order).Select(ToPublicQuestionDto).ToList(),
	};

	private static ChallengeQuestionDto ToQuestionDto(ChallengeQuestion q) => new()
	{
		Id = q.Id,
		Order = q.Order,
		Prompt = q.Prompt,
		OptionA = q.OptionA,
		OptionB = q.OptionB,
		OptionC = q.OptionC,
		OptionD = q.OptionD,
		CorrectOption = q.CorrectOption,
	};

	private static PublicChallengeQuestionDto ToPublicQuestionDto(ChallengeQuestion q) => new()
	{
		Id = q.Id,
		Order = q.Order,
		Prompt = q.Prompt,
		OptionA = q.OptionA,
		OptionB = q.OptionB,
		OptionC = q.OptionC,
		OptionD = q.OptionD,
	};
}

public sealed class ChallengeAttemptService(
	WaaoDbContext Db,
	GamificationEngine Gamification,
	IValidator<SubmitChallengeAttemptDto> SubmitValidator,
	IValidator<GrantChallengeXpDto> GrantValidator) : IChallengeAttemptService
{
	public async Task<PublicChallengeDto> StartAsync(Guid challengeId, Guid collaboratorId, CancellationToken ct = default)
	{
		var challenge = await Db.Challenges.Include(c => c.Questions).FirstOrDefaultAsync(c => c.Id == challengeId && c.IsPublished, ct)
			?? throw new KeyNotFoundException($"Challenge {challengeId} not found.");

		var attempt = new ChallengeAttempt
		{
			Id = Guid.CreateVersion7(),
			ChallengeId = challengeId,
			CollaboratorId = collaboratorId,
			StartedAt = DateTime.UtcNow,
		};

		Db.ChallengeAttempts.Add(attempt);
		await Db.SaveChangesAsync(ct);

		return ChallengeService.ToPublicDto(challenge);
	}

	public async Task<ChallengeAttemptResultDto> SubmitAsync(Guid attemptId, SubmitChallengeAttemptDto dto, Guid collaboratorId, CancellationToken ct = default)
	{
		await SubmitValidator.ValidateAndThrowAsync(dto, ct);

		var attempt = await Db.ChallengeAttempts
			.Include(a => a.Challenge)
			.ThenInclude(c => c.Questions)
			.FirstOrDefaultAsync(a => a.Id == attemptId, ct)
			?? throw new KeyNotFoundException($"Attempt {attemptId} not found.");

		if (attempt.CollaboratorId != collaboratorId)
			throw new UnauthorizedAccessException("This attempt belongs to another collaborator.");

		// Idempotent: return existing result if already submitted
		if (attempt.SubmittedAt is not null)
		{
			var existingAnswers = await Db.ChallengeAttemptAnswers.Where(a => a.AttemptId == attemptId).ToListAsync(ct);
			return BuildResult(attempt, existingAnswers);
		}

		var questions = attempt.Challenge.Questions.OrderBy(q => q.Order).ToList();
		var answers = new List<ChallengeAttemptAnswer>();

		foreach (var answer in dto.Answers)
		{
			var question = questions.FirstOrDefault(q => q.Id == answer.QuestionId);
			if (question is null) continue;

			var isCorrect = char.ToUpperInvariant(answer.SelectedOption) == char.ToUpperInvariant(question.CorrectOption);
			answers.Add(new ChallengeAttemptAnswer
			{
				Id = Guid.CreateVersion7(),
				AttemptId = attemptId,
				QuestionId = answer.QuestionId,
				SelectedOption = char.ToUpperInvariant(answer.SelectedOption),
				IsCorrect = isCorrect,
			});
		}

		Db.ChallengeAttemptAnswers.AddRange(answers);

		var correctCount = answers.Count(a => a.IsCorrect);
		var totalCount = questions.Count;
		var scorePct = totalCount > 0 ? (int)Math.Round((double)correctCount / totalCount * 100) : 0;

		attempt.SubmittedAt = DateTime.UtcNow;
		attempt.ScorePct = scorePct;
		attempt.Passed = scorePct >= attempt.Challenge.PassPercent;
		attempt.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return BuildResult(attempt, answers);
	}

	public async Task<IReadOnlyList<ChallengeAttemptDto>> ListPendingForReviewAsync(CancellationToken ct = default)
	{
		return await Db.ChallengeAttempts
			.Where(a => a.Passed && a.XpAwardedAt == null && a.SubmittedAt != null)
			.Include(a => a.Challenge)
			.Include(a => a.Collaborator)
			.OrderBy(a => a.SubmittedAt)
			.Select(a => MapAttemptDto(a))
			.ToListAsync(ct);
	}

	public async Task<ChallengeAttemptDto> GrantXpForAttemptAsync(Guid attemptId, GrantChallengeXpDto dto, Guid adminId, CancellationToken ct = default)
	{
		await GrantValidator.ValidateAndThrowAsync(dto, ct);

		var attempt = await Db.ChallengeAttempts
			.Include(a => a.Challenge)
			.Include(a => a.Collaborator)
			.FirstOrDefaultAsync(a => a.Id == attemptId, ct)
			?? throw new KeyNotFoundException($"Attempt {attemptId} not found.");

		if (attempt.XpAwardedAt is not null)
			return MapAttemptDto(attempt);

		await Gamification.RecordAsync(
			attempt.CollaboratorId,
			dto.Amount,
			XpSource.Admin,
			$"Challenge passed: {attempt.Challenge.Title} [Category: {attempt.Challenge.Category}] ({attempt.ScorePct}%)",
			attemptId,
			"ChallengeAttempt",
			ct);

		attempt.XpAwarded = dto.Amount;
		attempt.XpAwardedAt = DateTime.UtcNow;
		attempt.XpAwardedByAdminId = adminId;
		attempt.UpdatedAt = DateTime.UtcNow;

		await Db.SaveChangesAsync(ct);
		return MapAttemptDto(attempt);
	}

	public async Task<IReadOnlyList<ChallengeAttemptDto>> ListMyAttemptsAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		return await Db.ChallengeAttempts
			.Where(a => a.CollaboratorId == collaboratorId)
			.Include(a => a.Challenge)
			.Include(a => a.Collaborator)
			.OrderByDescending(a => a.StartedAt)
			.Select(a => MapAttemptDto(a))
			.ToListAsync(ct);
	}

	private static ChallengeAttemptResultDto BuildResult(ChallengeAttempt attempt, List<ChallengeAttemptAnswer> answers)
	{
		var perQuestion = answers.Select(a => new PerQuestionResultDto
		{
			QuestionId = a.QuestionId,
			Selected = a.SelectedOption,
			IsCorrect = a.IsCorrect,
			CorrectOption = a.Question?.CorrectOption ?? '\0',
		}).ToList();

		return new ChallengeAttemptResultDto
		{
			AttemptId = attempt.Id,
			ScorePct = attempt.ScorePct,
			Passed = attempt.Passed,
			CorrectCount = answers.Count(a => a.IsCorrect),
			TotalCount = answers.Count,
			PerQuestion = perQuestion,
		};
	}

	private static ChallengeAttemptDto MapAttemptDto(ChallengeAttempt a) => new()
	{
		Id = a.Id,
		ChallengeId = a.ChallengeId,
		ChallengeTitle = a.Challenge?.Title ?? string.Empty,
		ChallengeCategory = a.Challenge?.Category ?? string.Empty,
		ChallengeSuggestedXp = a.Challenge?.SuggestedXp,
		CollaboratorId = a.CollaboratorId,
		CollaboratorName = a.Collaborator?.FullName ?? string.Empty,
		StartedAt = a.StartedAt,
		SubmittedAt = a.SubmittedAt,
		ScorePct = a.ScorePct,
		Passed = a.Passed,
		XpAwarded = a.XpAwarded,
		XpAwardedAt = a.XpAwardedAt,
		XpAwardedByAdminId = a.XpAwardedByAdminId,
	};
}
