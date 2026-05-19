using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class CreateCareerEventValidator : AbstractValidator<CreateCareerEventDto>
{
	public CreateCareerEventValidator()
	{
		RuleFor(x => x.CollaboratorId)
			.NotEmpty().WithMessage("Collaborator id is required.");

		RuleFor(x => x.Type)
			.IsInEnum().WithMessage("Event type is not a valid value.");

		RuleFor(x => x.EventDate)
			.NotEmpty().WithMessage("Event date is required.")
			.GreaterThan(new DateOnly(1970, 1, 1))
			.LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
			.WithMessage("Event date cannot be in the future.");

		RuleFor(x => x.Title)
			.NotEmpty().WithMessage("Title is required.")
			.MaximumLength(200);

		RuleFor(x => x.Notes).MaximumLength(4_000);
		RuleFor(x => x.FromValue).MaximumLength(200);
		RuleFor(x => x.ToValue).MaximumLength(200);
		RuleFor(x => x.AttachmentUrl).MaximumLength(500);
	}
}
