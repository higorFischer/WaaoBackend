using FluentAssertions;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Validation;
using Xunit;

namespace Waao.Tests.Onboarding;

public sealed class CompleteOnboardingValidatorTests
{
	private static CompleteOnboardingDto Good() => new()
	{
		PhotoUrl = "https://x/y.png",
		Bio = "hi",
		Birthdate = new DateOnly(1990, 1, 1),
		DepartmentId = Guid.CreateVersion7(),
	};

	[Fact] public void Valid_Passes() => new CompleteOnboardingValidator().Validate(Good()).IsValid.Should().BeTrue();
	[Fact] public void EmptyPhoto_Fails() => new CompleteOnboardingValidator().Validate(Good() with { PhotoUrl = "" }).IsValid.Should().BeFalse();
	[Fact] public void EmptyBio_Fails() => new CompleteOnboardingValidator().Validate(Good() with { Bio = "" }).IsValid.Should().BeFalse();
	[Fact] public void BioTooLong_Fails() => new CompleteOnboardingValidator().Validate(Good() with { Bio = new string('x', 1001) }).IsValid.Should().BeFalse();
	[Fact] public void DefaultBirthdate_Fails() => new CompleteOnboardingValidator().Validate(Good() with { Birthdate = default }).IsValid.Should().BeFalse();
	[Fact] public void FutureBirthdate_Fails() => new CompleteOnboardingValidator().Validate(Good() with { Birthdate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid.Should().BeFalse();
	[Fact] public void EmptyDepartment_Fails() => new CompleteOnboardingValidator().Validate(Good() with { DepartmentId = Guid.Empty }).IsValid.Should().BeFalse();
}
