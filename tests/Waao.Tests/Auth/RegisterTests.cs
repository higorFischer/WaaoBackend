using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class RegisterTests
{
	[Fact]
	public async Task Register_CreatesUnverified_NoJwt_SendsEmail_AdminFromConfig()
	{
		var f = AuthServiceFactory.Create();
		var res = await f.Service.RegisterAsync(new RegisterDto { FullName = "Higor", Email = "higor@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });

		res.Status.Should().Be("verification_sent");
		res.Email.Should().Be("higor@waao.com.br");
		var c = await f.Db.Collaborators.SingleAsync();
		c.EmailVerified.Should().BeFalse();
		c.EmailVerificationToken.Should().NotBeNullOrEmpty();
		c.EmailVerificationTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
		c.RoleKind.Should().Be(CollaboratorRoleKind.Admin);
		f.LastEmail.Should().NotBeNull();
		f.LastEmail!.Value.VerifyUrl.Should().Contain("/verify-email?token=");
	}

	[Fact]
	public async Task Register_NonAdminEmail_IsCollaborator()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		(await f.Db.Collaborators.SingleAsync()).RoleKind.Should().Be(CollaboratorRoleKind.Collaborator);
	}

	[Fact]
	public async Task Register_DuplicateEmail_ThrowsValidationWithEmailProperty()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Dup", Email = "dup@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });

		var act = async () => await f.Service.RegisterAsync(new RegisterDto { FullName = "Dup2", Email = "dup@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });

		var ex = (await act.Should().ThrowAsync<FluentValidation.ValidationException>()).Which;
		ex.Errors.Should().ContainSingle(e => e.PropertyName == "email" && e.ErrorMessage == "Email is already in use.");
	}
}
