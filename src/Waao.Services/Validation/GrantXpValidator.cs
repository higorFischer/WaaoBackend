using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class GrantXpValidator : AbstractValidator<GrantXpDto>
{
	public GrantXpValidator()
	{
		RuleFor(x => x.Amount).NotEqual(0).WithMessage("Amount must be non-zero (negative allowed for corrections).");
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(280);
	}
}
