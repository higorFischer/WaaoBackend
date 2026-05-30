using FluentValidation;
using Waao.Services.Abstractions.Dtos.Badges;

namespace Waao.Services.Validation.Badges;

public class CreateBadgeDefinitionValidator : AbstractValidator<CreateBadgeDefinitionDto>
{
	public CreateBadgeDefinitionValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(60);

		RuleFor(x => x.IconEmoji)
			.NotEmpty()
			.MaximumLength(16);
	}
}

public class UpdateBadgeDefinitionValidator : AbstractValidator<UpdateBadgeDefinitionDto>
{
	public UpdateBadgeDefinitionValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty()
			.MaximumLength(60);

		RuleFor(x => x.IconEmoji)
			.NotEmpty()
			.MaximumLength(16);
	}
}
