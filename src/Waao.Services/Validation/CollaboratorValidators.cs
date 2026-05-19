using FluentValidation;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class CreateCollaboratorValidator : AbstractValidator<CreateCollaboratorDto>
{
	public CreateCollaboratorValidator()
	{
		RuleFor(x => x.FullName)
			.NotEmpty().WithMessage("Full name is required.")
			.MaximumLength(200);

		RuleFor(x => x.Email)
			.NotEmpty().WithMessage("Email is required.")
			.EmailAddress().WithMessage("Email is not a valid address.")
			.MaximumLength(200);

		RuleFor(x => x.Cpf)
			.Matches(@"^\d{3}\.?\d{3}\.?\d{3}-?\d{2}$")
			.When(x => !string.IsNullOrWhiteSpace(x.Cpf))
			.WithMessage("CPF must be 11 digits, optionally formatted as 000.000.000-00.");

		RuleFor(x => x.JoinDate)
			.NotEmpty().WithMessage("Join date is required.")
			.LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30))
			.WithMessage("Join date cannot be more than 30 days in the future.")
			.GreaterThan(new DateOnly(1970, 1, 1)).WithMessage("Join date is unreasonably old.");

		RuleFor(x => x.Birthdate)
			.LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
			.When(x => x.Birthdate.HasValue)
			.WithMessage("Birthdate must be in the past.");

		RuleFor(x => x.Birthdate)
			.GreaterThan(new DateOnly(1900, 1, 1))
			.When(x => x.Birthdate.HasValue)
			.WithMessage("Birthdate is unreasonably old.");

		RuleFor(x => x.Bio).MaximumLength(2_000);
		RuleFor(x => x.PhotoUrl).MaximumLength(500);
	}
}

public class UpdateCollaboratorValidator : AbstractValidator<UpdateCollaboratorDto>
{
	public UpdateCollaboratorValidator()
	{
		RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
		RuleFor(x => x.Bio).MaximumLength(2_000);
		RuleFor(x => x.PhotoUrl).MaximumLength(500);

		RuleFor(x => x.Status).IsInEnum();

		// If terminated, status must match and vice-versa
		RuleFor(x => x.TerminationDate)
			.NotNull()
			.When(x => x.Status == CollaboratorStatus.Terminated)
			.WithMessage("Termination date is required when status is Terminated.");

		RuleFor(x => x.TerminationDate)
			.LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
			.When(x => x.TerminationDate.HasValue)
			.WithMessage("Termination date cannot be in the future.");
	}
}
