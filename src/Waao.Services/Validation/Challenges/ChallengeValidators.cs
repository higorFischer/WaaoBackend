using FluentValidation;
using Waao.Services.Abstractions.Dtos.Challenges;

namespace Waao.Services.Validation.Challenges;

public class CreateChallengeValidator : AbstractValidator<CreateChallengeDto>
{
	public CreateChallengeValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
		RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
		RuleFor(x => x.SuggestedXp).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000).When(x => x.SuggestedXp.HasValue);
		RuleFor(x => x.PassPercent).InclusiveBetween(50, 100);
	}
}

public class UpdateChallengeValidator : AbstractValidator<UpdateChallengeDto>
{
	public UpdateChallengeValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
		RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
		RuleFor(x => x.SuggestedXp).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000).When(x => x.SuggestedXp.HasValue);
		RuleFor(x => x.PassPercent).InclusiveBetween(50, 100);
	}
}

public class CreateChallengeQuestionValidator : AbstractValidator<CreateChallengeQuestionDto>
{
	private static readonly char[] ValidOptions = ['A', 'B', 'C', 'D'];

	public CreateChallengeQuestionValidator()
	{
		RuleFor(x => x.Prompt).NotEmpty().MaximumLength(500);
		RuleFor(x => x.OptionA).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionB).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionC).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionD).NotEmpty().MaximumLength(200);
		RuleFor(x => x.CorrectOption).Must(o => ValidOptions.Contains(o)).WithMessage("CorrectOption must be A, B, C, or D.");
	}
}

public class UpdateChallengeQuestionValidator : AbstractValidator<UpdateChallengeQuestionDto>
{
	private static readonly char[] ValidOptions = ['A', 'B', 'C', 'D'];

	public UpdateChallengeQuestionValidator()
	{
		RuleFor(x => x.Prompt).NotEmpty().MaximumLength(500);
		RuleFor(x => x.OptionA).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionB).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionC).NotEmpty().MaximumLength(200);
		RuleFor(x => x.OptionD).NotEmpty().MaximumLength(200);
		RuleFor(x => x.CorrectOption).Must(o => ValidOptions.Contains(o)).WithMessage("CorrectOption must be A, B, C, or D.");
	}
}

public class SubmitChallengeAttemptValidator : AbstractValidator<SubmitChallengeAttemptDto>
{
	private static readonly char[] ValidOptions = ['A', 'B', 'C', 'D'];

	public SubmitChallengeAttemptValidator()
	{
		RuleFor(x => x.Answers).NotEmpty();
		RuleForEach(x => x.Answers).ChildRules(answer =>
		{
			answer.RuleFor(a => a.SelectedOption).Must(o => ValidOptions.Contains(o)).WithMessage("SelectedOption must be A, B, C, or D.");
		});
	}
}

public class GrantChallengeXpValidator : AbstractValidator<GrantChallengeXpDto>
{
	public GrantChallengeXpValidator()
	{
		RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000);
	}
}
