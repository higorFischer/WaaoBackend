using FluentValidation;
using Waao.Services.Abstractions.Dtos.Kudos;

namespace Waao.Services.Validation.Kudos;

public class GiveKudoValidator : AbstractValidator<GiveKudoDto>
{
	public GiveKudoValidator()
	{
		RuleFor(x => x.RecipientIds)
			.NotEmpty()
			.WithMessage("At least one recipient is required.");

		RuleFor(x => x.Message)
			.NotEmpty()
			.MaximumLength(500);
	}
}
