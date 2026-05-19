using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class LoginValidator : AbstractValidator<LoginDto>
{
	public LoginValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
		RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
	}
}

public class RegisterValidator : AbstractValidator<RegisterDto>
{
	public RegisterValidator()
	{
		RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
		RuleFor(x => x.Email)
			.Matches(@"^[^@\s]+@waao\.com\.br$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
			.WithMessage("Email must be a @waao.com.br address.");
		RuleFor(x => x.Password)
			.NotEmpty()
			.MinimumLength(8).WithMessage("Password must be at least 8 characters.")
			.MaximumLength(200);
		RuleFor(x => x.JoinDate)
			.NotEmpty()
			.LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30))
			.GreaterThan(new DateOnly(1970, 1, 1));
	}
}

public class VerifyEmailValidator : AbstractValidator<VerifyEmailDto>
{
	public VerifyEmailValidator() => RuleFor(x => x.Token).NotEmpty();
}

public class ResendVerificationValidator : AbstractValidator<ResendVerificationDto>
{
	public ResendVerificationValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
	public ChangePasswordValidator()
	{
		RuleFor(x => x.CurrentPassword).NotEmpty();
		RuleFor(x => x.NewPassword)
			.NotEmpty()
			.MinimumLength(8).WithMessage("New password must be at least 8 characters.")
			.NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from current.");
	}
}
