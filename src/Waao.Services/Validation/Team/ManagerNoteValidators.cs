using FluentValidation;
using Waao.Services.Abstractions.Dtos.Team;

namespace Waao.Services.Validation.Team;

public class CreateManagerNoteValidator : AbstractValidator<CreateManagerNoteDto>
{
	public CreateManagerNoteValidator()
	{
		RuleFor(x => x.Body)
			.NotEmpty().WithMessage("Note body is required.")
			.MaximumLength(8000);
	}
}

public class UpdateManagerNoteValidator : AbstractValidator<UpdateManagerNoteDto>
{
	public UpdateManagerNoteValidator()
	{
		RuleFor(x => x.Body)
			.NotEmpty().WithMessage("Note body is required.")
			.MaximumLength(8000);
	}
}
