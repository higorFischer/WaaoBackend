using FluentAssertions;
using Waao.Services.Abstractions.Dtos;
using Xunit;

namespace Waao.Tests.Onboarding;

public sealed class OnboardingDtoTests
{
	[Fact]
	public void OnboardingStatusDto_HasAllPerFieldFlags()
	{
		var dto = new OnboardingStatusDto
		{
			Completed = false,
			CompletedAt = null,
			PhotoSet = false,
			BioSet = false,
			BirthdateSet = false,
			DepartmentSet = false,
		};
		dto.Completed.Should().BeFalse();
		dto.PhotoSet.Should().BeFalse();
	}

	[Fact]
	public void CompleteOnboardingDto_HasFourRequiredFields()
	{
		var dto = new CompleteOnboardingDto
		{
			PhotoUrl = "https://x/y.png",
			Bio = "hi",
			Birthdate = new DateOnly(1990, 1, 1),
			DepartmentId = Guid.CreateVersion7(),
		};
		dto.PhotoUrl.Should().Be("https://x/y.png");
		dto.DepartmentId.Should().NotBeEmpty();
	}
}
