using FluentValidation;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Services.Validation;

namespace Waao.Tests.Support;

public static class AdminServiceFactory
{
	public static AdminService Create(WaaoDbContext db)
		=> new(db, new StreakTracker(db), new BadgeEvaluator(db), new GamificationEngine(db), new GrantXpValidator(), new AdminCreateUserValidator(), new AdminUpdateUserValidator(), new AdminResetPasswordValidator());
}
