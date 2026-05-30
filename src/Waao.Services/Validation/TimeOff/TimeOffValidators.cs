using FluentValidation;
using Waao.Services.Abstractions.Dtos.TimeOff;

namespace Waao.Services.Validation.TimeOff;

public class CreateTimeOffValidator : AbstractValidator<CreateTimeOffDto>
{
	public CreateTimeOffValidator()
	{
		RuleFor(x => x.EndDate)
			.GreaterThanOrEqualTo(x => x.StartDate)
			.WithMessage("EndDate must be on or after StartDate.");

		RuleFor(x => x)
			.Must(x => (x.EndDate.DayNumber - x.StartDate.DayNumber + 1) <= 365)
			.WithMessage("Time off request cannot span more than 365 days.")
			.OverridePropertyName("EndDate");

		RuleFor(x => x.Reason)
			.MaximumLength(500)
			.When(x => x.Reason is not null);
	}
}
