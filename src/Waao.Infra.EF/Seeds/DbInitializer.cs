using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;

namespace Waao.Infra.EF.Seeds;

public static class DbInitializer
{
	public static async Task SeedAsync(WaaoDbContext db, CancellationToken ct = default)
	{
		await SeedLevelsAsync(db, ct);
		await SeedBadgesAsync(db, ct);
		await SeedDepartmentsAsync(db, ct);
		await SeedDefaultAdminsAsync(db, ct);
		await db.SaveChangesAsync(ct);
	}

	// ---------- Levels (0 -> 50k XP) ----------
	private static async Task SeedLevelsAsync(WaaoDbContext db, CancellationToken ct)
	{
		if (await db.LevelDefinitions.AnyAsync(ct)) return;

		(int Level, long Xp, string Title, string Icon, string Color)[] levels =
		[
			(1,      0, "Newcomer",        "🌱", "#94A3B8"),
			(2,    100, "Rookie",          "🌿", "#22C55E"),
			(3,    300, "Learner",         "📚", "#22C55E"),
			(4,    600, "Contributor",     "🤝", "#14B8A6"),
			(5,  1_000, "Specialist",      "🛠️", "#14B8A6"),
			(6,  1_500, "Practitioner",    "⚡", "#3B82F6"),
			(7,  2_100, "Builder",         "🏗️", "#3B82F6"),
			(8,  2_800, "Catalyst",        "🔥", "#6366F1"),
			(9,  3_600, "Mentor",          "🧭", "#6366F1"),
			(10, 4_500, "Expert",          "🎯", "#8B5CF6"),
			(11, 5_500, "Innovator",       "💡", "#8B5CF6"),
			(12, 6_600, "Leader",          "🗺️", "#A855F7"),
			(13, 7_800, "Architect",       "🏛️", "#A855F7"),
			(14, 9_100, "Strategist",      "♟️", "#D946EF"),
			(15, 10_500, "Principal",      "🏔️", "#D946EF"),
			(16, 13_000, "Vanguard",       "🚀", "#EC4899"),
			(17, 16_000, "Luminary",       "🌟", "#EC4899"),
			(18, 20_000, "Trailblazer",    "🌠", "#F59E0B"),
			(19, 25_000, "Icon",           "👑", "#F59E0B"),
			(20, 50_000, "WAAO Legend",    "🏆", "#EAB308"),
		];

		foreach (var (level, xp, title, icon, color) in levels)
		{
			db.LevelDefinitions.Add(new LevelDefinition
			{
				Id = Guid.CreateVersion7(),
				Level = level,
				XpThreshold = xp,
				Title = title,
				IconEmoji = icon,
				ColorHex = color,
			});
		}
	}

	// ---------- Badges ----------
	private static async Task SeedBadgesAsync(WaaoDbContext db, CancellationToken ct)
	{
		if (await db.Badges.AnyAsync(ct)) return;

		(string Code, string Name, string Desc, string Icon, BadgeCategory Cat, BadgeRarity Rarity, int Xp, string Rule)[] badges =
		[
			// Tenure
			("TENURE_90_DAYS",  "First Quarter",    "Complete 90 days at WAAO.",          "🗓️", BadgeCategory.Tenure, BadgeRarity.Common,    100, "90 days since hire date"),
			("TENURE_1_YEAR",   "One Year Strong",  "Complete 1 year at WAAO.",           "🎂", BadgeCategory.Tenure, BadgeRarity.Uncommon,  300, "365 days since hire date"),
			("TENURE_3_YEARS",  "Rooted",           "Complete 3 years at WAAO.",          "🌳", BadgeCategory.Tenure, BadgeRarity.Rare,      500, "3 years since hire date"),
			("TENURE_5_YEARS",  "Pillar",           "Complete 5 years at WAAO.",          "🏛️", BadgeCategory.Tenure, BadgeRarity.Epic,     1000, "5 years since hire date"),
			("TENURE_10_YEARS", "Decade",           "Complete 10 years at WAAO.",         "💎", BadgeCategory.Tenure, BadgeRarity.Legendary, 2500, "10 years since hire date"),

			// Career
			("FIRST_PROMOTION", "First Step Up",    "Earn your first promotion.",         "⬆️", BadgeCategory.Career, BadgeRarity.Uncommon,  250, "First Promotion event"),
			("TRIPLE_PROMOTED", "On the Rise",      "Earn three promotions.",             "🚀", BadgeCategory.Career, BadgeRarity.Epic,      750, "3 total Promotion events"),
			("LATERAL_MOVE",    "Explorer",         "Make a lateral move across teams.",  "🧭", BadgeCategory.Career, BadgeRarity.Uncommon,  200, "First Lateral event"),
			("PERF_REVIEW",     "Reflective",       "Complete a performance review.",     "📝", BadgeCategory.Career, BadgeRarity.Common,    100, "First PerformanceReview event"),
			("DEPT_CHANGE",     "Reinvented",       "Move to a new department.",          "🔄", BadgeCategory.Career, BadgeRarity.Rare,      300, "First DepartmentChange event"),

			// Learning
			("FIRST_TRAINING",  "Curious Mind",     "Complete your first training.",      "📚", BadgeCategory.Learning, BadgeRarity.Common,   100, "First Training event"),
			("TEN_TRAININGS",   "Perpetual Student","Complete 10 trainings.",             "🎓", BadgeCategory.Learning, BadgeRarity.Rare,     500, "10 total Training events"),
			("FIRST_CERT",      "Certified",        "Earn your first certification.",     "🏆", BadgeCategory.Learning, BadgeRarity.Uncommon, 250, "First Certification event"),
			("FIVE_CERTS",      "Credentialed",     "Earn 5 certifications.",             "🎖️", BadgeCategory.Learning, BadgeRarity.Epic,    1000, "5 total Certification events"),
			("TEN_CERTS",       "Scholar",          "Earn 10 certifications.",            "📜", BadgeCategory.Learning, BadgeRarity.Legendary, 2500, "10 total Certification events"),

			// Activity (streaks)
			("STREAK_7",        "Week One",         "Maintain a 7-day activity streak.",  "🔥", BadgeCategory.Activity, BadgeRarity.Common,    50, "7-day streak"),
			("STREAK_30",       "Consistent",       "Maintain a 30-day activity streak.", "🔥🔥", BadgeCategory.Activity, BadgeRarity.Uncommon, 150, "30-day streak"),
			("STREAK_90",       "Relentless",       "Maintain a 90-day activity streak.", "🔥🔥🔥", BadgeCategory.Activity, BadgeRarity.Rare,    400, "90-day streak"),
			("STREAK_365",      "Unstoppable",      "Maintain a 365-day activity streak.","🌋", BadgeCategory.Activity, BadgeRarity.Legendary,2000, "365-day streak"),

			// Community
			("FIRST_KUDOS",     "Appreciated",      "Receive your first peer kudos.",     "👏", BadgeCategory.Community, BadgeRarity.Common,    50, "First Kudos event"),
			("TEN_KUDOS",       "Team Player",      "Receive 10 peer kudos.",             "🤝", BadgeCategory.Community, BadgeRarity.Rare,     300, "10 total Kudos events"),
			("MANAGER",         "People First",     "Become someone's manager.",          "🧑‍🏫", BadgeCategory.Community, BadgeRarity.Uncommon, 250, "First direct report assigned"),

			// Special
			("FOUNDER",         "Day One",          "Join WAAO in its first year.",       "🌅", BadgeCategory.Special, BadgeRarity.Legendary, 1500, "Hired during company year 1"),
			("WELCOME",         "Welcome Aboard",   "Officially join WAAO.",              "🎉", BadgeCategory.Special, BadgeRarity.Common,    100, "Hire event"),

			// Activity (login)
			("FIRST_LOGIN",     "Hello WAAO",       "Sign in for the first time.",        "👋", BadgeCategory.Activity, BadgeRarity.Common,    50, "First successful login"),
			("LOGIN_7",         "Engaged",          "Log in 7 days in a row.",            "📅", BadgeCategory.Activity, BadgeRarity.Common,    75, "7-day login streak"),
			("LOGIN_30",        "Devoted",          "Log in 30 days in a row.",           "📆", BadgeCategory.Activity, BadgeRarity.Uncommon, 200, "30-day login streak"),
			("LOGIN_90",        "Habit Loop",       "Log in 90 days in a row.",           "🗓️", BadgeCategory.Activity, BadgeRarity.Rare,     500, "90-day login streak"),
			("LOGIN_365",       "Always On",        "Log in every day for a year.",       "🔆", BadgeCategory.Activity, BadgeRarity.Legendary,2500, "365-day login streak"),

			// Profile completion
			("PROFILE_COMPLETE","All About You",    "Fill in photo, bio and birthdate.",  "🪪", BadgeCategory.Special, BadgeRarity.Common,    100, "Photo + bio + birthdate set"),

			// Kanban
			("FIRST_CARD_DONE", "First Card Done",  "Move your first kanban card to done.","✅", BadgeCategory.Career,   BadgeRarity.Common,   100, "First kanban card completed"),
			("WORKHORSE",       "Workhorse",        "Complete 10 cards in any single week.","💪", BadgeCategory.Career,   BadgeRarity.Uncommon, 300, "10 cards completed within 7 days"),
			("EPIC_COMPLETE",   "Epic Complete",    "Close every card in an epic.",       "🏆", BadgeCategory.Career,   BadgeRarity.Rare,     500, "All cards in an epic completed"),
			("THOROUGH",        "Thorough",         "Leave 10 comments across cards.",    "💬", BadgeCategory.Community, BadgeRarity.Common,  150, "10 kanban comments authored"),
		];

		foreach (var (code, name, desc, icon, cat, rarity, xp, rule) in badges)
		{
			db.Badges.Add(new Badge
			{
				Id = Guid.CreateVersion7(),
				Code = code,
				Name = name,
				Description = desc,
				IconEmoji = icon,
				Category = cat,
				Rarity = rarity,
				XpReward = xp,
				UnlockRule = rule,
			});
		}
	}

	// ---------- Departments ----------
	private static async Task SeedDepartmentsAsync(WaaoDbContext db, CancellationToken ct)
	{
		if (await db.Departments.AnyAsync(ct)) return;

		(string Name, string Desc, string Color)[] departments =
		[
			("Engineering", "Software and platform teams.", "#6366F1"),
			("Product",     "Product management and design.", "#EC4899"),
			("People",      "HR, talent, operations.", "#10B981"),
			("Sales",       "Revenue and commercial.", "#F59E0B"),
			("Finance",     "Finance and accounting.", "#3B82F6"),
		];

		foreach (var (name, desc, color) in departments)
		{
			db.Departments.Add(new Department
			{
				Id = Guid.CreateVersion7(),
				Name = name,
				Description = desc,
				ColorHex = color,
			});
		}
	}

	// ---------- Default users (dev convenience) ----------
	private static async Task SeedDefaultAdminsAsync(WaaoDbContext db, CancellationToken ct)
	{
		(string Email, string Name, string Password, CollaboratorRoleKind Role, DateOnly JoinDate)[] users =
		[
			("higor@waao.com.br", "Higor", "Waao2026!", CollaboratorRoleKind.Admin, new DateOnly(2023, 1, 1)),
		];

		foreach (var (email, name, password, role, joinDate) in users)
		{
			if (await db.Collaborators.AnyAsync(c => c.Email == email, ct)) continue;

			db.Collaborators.Add(new Collaborator
			{
				Id = Guid.CreateVersion7(),
				FullName = name,
				Email = email,
				JoinDate = joinDate,
				RoleKind = role,
				PasswordHash = HashPassword(password),
				OnboardingCompletedAt = DateTime.UtcNow,
				EmailVerified = true,
				EmailVerifiedAt = DateTime.UtcNow,
			});
		}
	}

	private static string HashPassword(string password)
	{
		var salt = RandomNumberGenerator.GetBytes(16);
		var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
		return $"v1.200000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
	}
}
