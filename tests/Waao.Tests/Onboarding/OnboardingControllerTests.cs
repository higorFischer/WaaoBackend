using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Waao.Tests.Onboarding;

public sealed class OnboardingControllerTests
{
	private static readonly Type Type = typeof(Waao.API.Controllers.OnboardingController);

	[Fact]
	public void Class_IsApiControllerAuthorizedAndRouted()
	{
		Type.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
		Type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
		Type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/waao/onboarding");
	}

	[Fact]
	public void GetStatus_IsHttpGet_status()
	{
		var m = Type.GetMethod("GetStatus");
		m.Should().NotBeNull();
		m!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("status");
	}

	[Fact]
	public void Complete_IsHttpPost_complete()
	{
		var m = Type.GetMethod("Complete");
		m.Should().NotBeNull();
		m!.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("complete");
	}
}
