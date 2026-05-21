using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class AdminUpdateUserValidator : AbstractValidator<AdminUpdateUserDto>
{
	public AdminUpdateUserValidator()
	{
		RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(120);
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(160);
	}
}

public class AdminResetPasswordValidator : AbstractValidator<AdminResetPasswordDto>
{
	public AdminResetPasswordValidator()
	{
		RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
			.WithMessage("Password must be between 8 and 128 characters.");
	}
}
