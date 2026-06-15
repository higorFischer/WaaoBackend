using FluentValidation;
using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Validation.Team;

public class CreateSkillValidator : AbstractValidator<CreateSkillDto>
{
	public CreateSkillValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty().WithMessage("Skill name is required.")
			.MaximumLength(120);

		RuleFor(x => x.Category).MaximumLength(80);
	}
}

public class UpdateSkillValidator : AbstractValidator<UpdateSkillDto>
{
	public UpdateSkillValidator()
	{
		RuleFor(x => x.Name)
			.NotEmpty().WithMessage("Skill name is required.")
			.MaximumLength(120);

		RuleFor(x => x.Category).MaximumLength(80);
	}
}

public class UpsertCollaboratorSkillValidator : AbstractValidator<UpsertCollaboratorSkillDto>
{
	public UpsertCollaboratorSkillValidator()
	{
		RuleFor(x => x.Level)
			.IsInEnum().WithMessage("Skill level is not a valid value.");

		RuleFor(x => x.Note).MaximumLength(1000);
	}
}
