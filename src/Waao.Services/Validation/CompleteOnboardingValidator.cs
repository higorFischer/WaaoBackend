using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingDto>
{
	public CompleteOnboardingValidator()
	{
		RuleFor(x => x.PhotoUrl).NotEmpty().MaximumLength(500);
		RuleFor(x => x.Bio).NotEmpty().MaximumLength(1000);
		RuleFor(x => x.Birthdate)
			.NotEqual(default(DateOnly)).WithMessage("Birthdate is required.")
			.Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Birthdate must be in the past.");
		RuleFor(x => x.DepartmentId).NotEqual(Guid.Empty);
	}
}
