using FluentAssertions;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Validation;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class AuthValidatorsTests
{
	[Theory]
	[InlineData("alice@waao.com.br", true)]
	[InlineData("Bob@WAAO.COM.BR", true)]
	[InlineData("eve@gmail.com", false)]
	[InlineData("x@waao.com", false)]
	public void Register_OnlyAcceptsWaaoComBr(string email, bool valid)
	{
		var v = new RegisterValidator();
		var r = v.Validate(new RegisterDto { FullName = "T", Email = email, Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		r.IsValid.Should().Be(valid);
	}

	[Fact]
	public void VerifyEmail_RequiresToken()
		=> new VerifyEmailValidator().Validate(new VerifyEmailDto { Token = "" }).IsValid.Should().BeFalse();

	[Fact]
	public void Resend_RequiresValidEmail()
		=> new ResendVerificationValidator().Validate(new ResendVerificationDto { Email = "nope" }).IsValid.Should().BeFalse();
}
