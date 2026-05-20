namespace Waao.Services.Abstractions.Dtos.Challenges;

public record ChallengeQuestionDto
{
	public Guid Id { get; init; }
	public int Order { get; init; }
	public string Prompt { get; init; } = string.Empty;
	public string OptionA { get; init; } = string.Empty;
	public string OptionB { get; init; } = string.Empty;
	public string OptionC { get; init; } = string.Empty;
	public string OptionD { get; init; } = string.Empty;
	public char CorrectOption { get; init; }
}

public record PublicChallengeQuestionDto
{
	public Guid Id { get; init; }
	public int Order { get; init; }
	public string Prompt { get; init; } = string.Empty;
	public string OptionA { get; init; } = string.Empty;
	public string OptionB { get; init; } = string.Empty;
	public string OptionC { get; init; } = string.Empty;
	public string OptionD { get; init; } = string.Empty;
}

public record ChallengeDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string Category { get; init; } = string.Empty;
	public int? SuggestedXp { get; init; }
	public int PassPercent { get; init; }
	public bool IsPublished { get; init; }
	public Guid CreatedById { get; init; }
	public DateTime CreatedAt { get; init; }
	public IReadOnlyList<ChallengeQuestionDto> Questions { get; init; } = [];
}

public record PublicChallengeDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string Category { get; init; } = string.Empty;
	public int? SuggestedXp { get; init; }
	public int PassPercent { get; init; }
	public bool IsPublished { get; init; }
	public DateTime CreatedAt { get; init; }
	public IReadOnlyList<PublicChallengeQuestionDto> Questions { get; init; } = [];
}

public record CreateChallengeDto
{
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string Category { get; init; } = string.Empty;
	public int? SuggestedXp { get; init; }
	public int PassPercent { get; init; } = 70;
}

public record UpdateChallengeDto
{
	public string Title { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string Category { get; init; } = string.Empty;
	public int? SuggestedXp { get; init; }
	public int PassPercent { get; init; } = 70;
}

public record CreateChallengeQuestionDto
{
	public int Order { get; init; }
	public string Prompt { get; init; } = string.Empty;
	public string OptionA { get; init; } = string.Empty;
	public string OptionB { get; init; } = string.Empty;
	public string OptionC { get; init; } = string.Empty;
	public string OptionD { get; init; } = string.Empty;
	public char CorrectOption { get; init; }
}

public record UpdateChallengeQuestionDto
{
	public int Order { get; init; }
	public string Prompt { get; init; } = string.Empty;
	public string OptionA { get; init; } = string.Empty;
	public string OptionB { get; init; } = string.Empty;
	public string OptionC { get; init; } = string.Empty;
	public string OptionD { get; init; } = string.Empty;
	public char CorrectOption { get; init; }
}

public record SubmitAnswerDto
{
	public Guid QuestionId { get; init; }
	public char SelectedOption { get; init; }
}

public record SubmitChallengeAttemptDto
{
	public IReadOnlyList<SubmitAnswerDto> Answers { get; init; } = [];
}

public record PerQuestionResultDto
{
	public Guid QuestionId { get; init; }
	public char Selected { get; init; }
	public bool IsCorrect { get; init; }
	public char CorrectOption { get; init; }
}

public record ChallengeAttemptResultDto
{
	public Guid AttemptId { get; init; }
	public int ScorePct { get; init; }
	public bool Passed { get; init; }
	public int CorrectCount { get; init; }
	public int TotalCount { get; init; }
	public IReadOnlyList<PerQuestionResultDto> PerQuestion { get; init; } = [];
}

public record ChallengeAttemptDto
{
	public Guid Id { get; init; }
	public Guid ChallengeId { get; init; }
	public string ChallengeTitle { get; init; } = string.Empty;
	public string ChallengeCategory { get; init; } = string.Empty;
	public int? ChallengeSuggestedXp { get; init; }
	public Guid CollaboratorId { get; init; }
	public string CollaboratorName { get; init; } = string.Empty;
	public DateTime StartedAt { get; init; }
	public DateTime? SubmittedAt { get; init; }
	public int ScorePct { get; init; }
	public bool Passed { get; init; }
	public int? XpAwarded { get; init; }
	public DateTime? XpAwardedAt { get; init; }
	public Guid? XpAwardedByAdminId { get; init; }
}

public record GrantChallengeXpDto
{
	public int Amount { get; init; }
}

public record PublishChallengeDto
{
	public bool IsPublished { get; init; }
}

public record ReorderQuestionsDto
{
	public IReadOnlyList<Guid> OrderedIds { get; init; } = [];
}
