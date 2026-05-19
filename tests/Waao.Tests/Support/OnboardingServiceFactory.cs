using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Services;
using Waao.Services.Validation;

namespace Waao.Tests.Support;

public static class OnboardingServiceFactory
{
	public static (OnboardingService Service, WaaoDbContext Db) Create()
	{
		var db = TestDb.New();
		IValidator<CompleteOnboardingDto> v = new CompleteOnboardingValidator();
		var svc = new OnboardingService(db, v, NullLogger<OnboardingService>.Instance);
		return (svc, db);
	}
}
