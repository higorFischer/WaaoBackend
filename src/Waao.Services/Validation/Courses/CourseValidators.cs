using FluentValidation;
using Waao.Services.Abstractions.Dtos.Courses;

namespace Waao.Services.Validation.Courses;

public class CreateCourseValidator : AbstractValidator<CreateCourseDto>
{
	public CreateCourseValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
		RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
		RuleFor(x => x.MaterialUrl).MaximumLength(500).When(x => x.MaterialUrl is not null);
		RuleFor(x => x.Provider).MaximumLength(120).When(x => x.Provider is not null);
		RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000).When(x => x.DurationMinutes.HasValue);
		RuleFor(x => x.SuggestedXp).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000).When(x => x.SuggestedXp.HasValue);
	}
}

public class UpdateCourseValidator : AbstractValidator<UpdateCourseDto>
{
	public UpdateCourseValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
		RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
		RuleFor(x => x.MaterialUrl).MaximumLength(500).When(x => x.MaterialUrl is not null);
		RuleFor(x => x.Provider).MaximumLength(120).When(x => x.Provider is not null);
		RuleFor(x => x.DurationMinutes).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000).When(x => x.DurationMinutes.HasValue);
		RuleFor(x => x.SuggestedXp).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000).When(x => x.SuggestedXp.HasValue);
	}
}

public class MarkCourseCompleteValidator : AbstractValidator<MarkCourseCompleteDto>
{
	public MarkCourseCompleteValidator()
	{
		RuleFor(x => x.Notes).MaximumLength(500).When(x => x.Notes is not null);
	}
}

public class GrantCourseXpValidator : AbstractValidator<GrantCourseXpDto>
{
	public GrantCourseXpValidator()
	{
		RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000);
	}
}
