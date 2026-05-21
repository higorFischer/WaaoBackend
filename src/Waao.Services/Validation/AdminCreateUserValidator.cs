using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class AdminCreateUserValidator : AbstractValidator<AdminCreateUserDto>
{
	public AdminCreateUserValidator()
	{
		RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(120);
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(160);
		RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
			.WithMessage("Password must be at least 8 characters.");
		RuleFor(x => x.RoleKind).IsInEnum();
	}
}
