using FluentAssertions;
using Waao.Domain.Models.Entities;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class EmailVerificationFieldsTests
{
	[Fact]
	public void NewCollaborator_DefaultsToUnverified()
	{
		var c = new Collaborator();
		c.EmailVerified.Should().BeFalse();
		c.EmailVerificationToken.Should().BeNull();
		c.EmailVerificationTokenExpiresAt.Should().BeNull();
		c.EmailVerifiedAt.Should().BeNull();
		c.LastVerificationEmailSentAt.Should().BeNull();
	}
}
