using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Services.Abstractions;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class VerifyResendLoginTests
{
	[Fact]
	public async Task Login_Unverified_Throws_Then_Works_After_Verify()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		var token = (await f.Db.Collaborators.SingleAsync()).EmailVerificationToken!;

		var login = async () => await f.Service.LoginAsync(new LoginDto { Email = "al@waao.com.br", Password = "Sup3rSecret!" });
		await login.Should().ThrowAsync<EmailNotVerifiedException>();

		var auth = await f.Service.VerifyEmailAsync(new VerifyEmailDto { Token = token });
		auth.Token.Should().NotBeNullOrEmpty();
		var c = await f.Db.Collaborators.SingleAsync();
		c.EmailVerified.Should().BeTrue();
		c.EmailVerificationToken.Should().BeNull();

		var ok = await f.Service.LoginAsync(new LoginDto { Email = "al@waao.com.br", Password = "Sup3rSecret!" });
		ok.Token.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Verify_BadToken_Throws()
	{
		var f = AuthServiceFactory.Create();
		var act = async () => await f.Service.VerifyEmailAsync(new VerifyEmailDto { Token = "nope" });
		await act.Should().ThrowAsync<InvalidVerificationTokenException>();
	}

	[Fact]
	public async Task Verify_ExpiredToken_Throws()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		var c = await f.Db.Collaborators.SingleAsync();
		var token = c.EmailVerificationToken!;
		c.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
		await f.Db.SaveChangesAsync();

		var act = async () => await f.Service.VerifyEmailAsync(new VerifyEmailDto { Token = token });
		await act.Should().ThrowAsync<InvalidVerificationTokenException>();
	}

	[Fact]
	public async Task Resend_NeverThrows_AndIsRateLimited()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		var act = async () => await f.Service.ResendVerificationAsync(new ResendVerificationDto { Email = "ghost@waao.com.br" });
		await act.Should().NotThrowAsync();
		var before = f.SentCount;
		await f.Service.ResendVerificationAsync(new ResendVerificationDto { Email = "al@waao.com.br" });
		f.SentCount.Should().Be(before); // rate-limited: register set LastVerificationEmailSentAt just now (<60s)
	}
}
