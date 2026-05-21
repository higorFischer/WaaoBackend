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

		// Courses and Challenges depend on the admin user existing.
		await SeedDefaultCoursesAsync(db, ct);
		await SeedGitCoursesAsync(db, ct);
		await SeedDefaultChallengesAsync(db, ct);
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

	// ---------- Courses (30 published, spread across 6 SkillRadar axes) ----------
	private static async Task SeedDefaultCoursesAsync(WaaoDbContext db, CancellationToken ct)
	{
		if (await db.Courses.AnyAsync(ct)) return;

		var author = await db.Collaborators.FirstOrDefaultAsync(c => c.Email == "higor@waao.com.br", ct);
		if (author is null) return;

		(string Title, string Description, string Category, string? Provider, int Duration, int SuggestedXp)[] courses =
		[
			// Backend (keywords: backend, api, database, service, sql, infra)
			("Building REST APIs with .NET 9", "Learn to design and implement RESTful backend APIs using ASP.NET Core 9 minimal APIs, controllers, and OpenAPI documentation.", "Backend services", "WAAO Learning", 120, 200),
			("EF Core 9 & PostgreSQL Mastery", "Deep dive into Entity Framework Core 9: migrations, query filters, owned types, value converters, and Npgsql-specific features. Practical SQL tips included.", "Backend database", "WAAO Learning", 180, 300),
			("Clean Architecture in .NET", "Implement clean architecture layers — Domain, Application, Infra — in a real .NET 9 backend service. Understand dependency inversion, ports & adapters.", "Backend architecture", "WAAO Learning", 150, 250),
			("Domain-Driven Design Fundamentals", "Entities, value objects, aggregates, domain events, and repositories explained through .NET 9 examples from the WAAO backend.", "Backend api", "WAAO Learning", 90, 150),
			("Async .NET: Channels & Task Parallel Library", "Write high-throughput backend service code with System.Threading.Channels, Parallel.ForEachAsync, and proper cancellation token propagation.", "Backend infra", "WAAO Learning", 100, 200),
			("SQL Query Optimization for Developers", "Understand EXPLAIN ANALYZE, index strategies, partial indexes, and common SQL anti-patterns. Hands-on with the WaaoLocal PostgreSQL database.", "Backend sql", "WAAO Learning", 120, 200),

			// Frontend (keywords: frontend, ui, css, component, page, design)
			("React 19 Foundations", "Master the new React 19 APIs: use(), Actions, optimistic updates, and the new compiler. Build production-grade frontend components step by step.", "Frontend React", "WAAO Learning", 120, 200),
			("TypeScript for Frontend Engineers", "From basic types to generics, conditional types, mapped types, and strict mode. Write zero-any TypeScript in a real frontend codebase.", "Frontend TypeScript", "WAAO Learning", 90, 150),
			("TanStack Query v5 in Practice", "Data fetching, caching, mutations, optimistic updates, and background refetching in a React 19 frontend. Replace useEffect+fetch forever.", "Frontend ui data", "WAAO Learning", 80, 150),
			("Design Systems with Tailwind & Radix UI", "Build a scalable component library using Tailwind CSS, class-variance-authority, and Radix UI primitives. CSS design tokens and theming included.", "Frontend design css", "WAAO Learning", 110, 200),
			("React Hook Form + Zod", "Schema-driven forms with zero uncontrolled state, proper validation messages, and seamless API integration in a React frontend page.", "Frontend component forms", "WAAO Learning", 70, 120),
			("Vite Build Optimization", "Tree-shaking, code splitting, dynamic imports, build analysis, and Cloudflare Worker deployment strategies for frontend applications.", "Frontend build", "WAAO Learning", 60, 100),

			// DevOps (keywords: devops, deploy, docker, ci/cd, cloud, pipeline)
			("Fly.io for .NET Developers", "Deploy a .NET 9 API to Fly.io: fly launch, Dockerfile, fly.toml, machine sizing, secrets, and zero-downtime rolling deploys.", "DevOps deploy", "WAAO Learning", 90, 150),
			("GitHub Actions CI/CD Pipelines", "Build a complete CI/CD pipeline for build, test, and deploy stages using GitHub Actions. Matrix builds, caching, and environment secrets.", "DevOps ci/cd pipeline", "WAAO Learning", 100, 150),
			("Docker for Backend Engineers", "Containers from first principles: Dockerfile multi-stage builds, .dockerignore, layer caching, and docker-compose for local development.", "DevOps docker", "WAAO Learning", 90, 150),
			("PostgreSQL in Production", "Backup strategies (pg_dump → R2), monitoring with pg_stat_statements, connection pooling with PgBouncer, and safe schema migrations on live cloud databases.", "DevOps database cloud", "WAAO Learning", 120, 200),
			("Cloudflare Workers & Pages", "Deploy static frontends and lightweight edge functions on Cloudflare. Wrangler CLI, KV storage, R2 buckets, and Workers routes.", "DevOps cloud deploy", "WAAO Learning", 80, 130),
			("Infrastructure as Code with Pulumi", "Define Fly.io apps, Cloudflare zones, and Postgres clusters in TypeScript using Pulumi. State management and drift detection.", "DevOps infra cloud", "WAAO Learning", 120, 200),

			// Quality (keywords: test, review, bug, qa, quality, coverage)
			("Unit Testing in .NET with xUnit", "Write fast, isolated unit tests using xUnit, Moq, and FluentAssertions. Test services, validators, and domain logic with EF Core InMemory.", "Quality test", "WAAO Learning", 90, 150),
			("Integration Testing with EF Core", "Test real database behavior using Testcontainers for PostgreSQL and WebApplicationFactory. Avoid mock hell while keeping tests deterministic.", "Quality test coverage", "WAAO Learning", 100, 180),
			("Code Review Best Practices", "Structured code review methodology: what to look for, how to give feedback, checklists, and anti-patterns to avoid. Improve team quality through review.", "Quality review", "WAAO Learning", 60, 100),
			("Bug Hunting & Debugging .NET APIs", "Systematic debugging: reading stack traces, using dotnet-dump, EF Core logging, and correlating logs in production. Bug reproduction strategies.", "Quality bug debugging", "WAAO Learning", 80, 130),
			("Test Coverage & Quality Metrics", "Measure what matters: branch coverage vs statement coverage, mutation testing, and quality gates in CI/CD. Avoid coverage theater.", "Quality qa coverage", "WAAO Learning", 60, 100),

			// Communication (keywords: docs, present, comm, write, share, talk)
			("Technical Writing for Developers", "Write clear API docs, ADRs, runbooks, and onboarding guides. Markdown structure, diagrams with Mermaid, and docs-as-code practices.", "Communication docs write", "WAAO Learning", 80, 130),
			("Presenting Technical Work Effectively", "Structure and deliver technical presentations, demos, and post-mortems to mixed audiences. Slide design, storytelling, and handling Q&A.", "Communication present talk", "WAAO Learning", 60, 100),
			("OpenAPI & Swagger Documentation", "Author high-quality API docs with OpenAPI 3.1. Descriptions, examples, error schemas, and generating client SDKs from the spec.", "Communication docs api", "WAAO Learning", 70, 120),
			("Writing Good Commit Messages & PRs", "Conventional commits, PR descriptions, changelog generation, and semantic versioning. Write commit messages that help future engineers.", "Communication write share", "WAAO Learning", 40, 60),
			("Remote Communication for Distributed Teams", "Async-first communication norms: structured updates, written decision logs, and meeting facilitation for distributed engineering teams.", "Communication comm share", "WAAO Learning", 50, 80),

			// Leadership (keywords: lead, mentor, onboard, guide, coach)
			("Engineering Leadership Fundamentals", "Moving from IC to lead: 1:1s, project scoping, unblocking the team, and giving technical direction without micromanaging.", "Leadership lead guide", "WAAO Learning", 90, 150),
			("Mentoring Junior Developers", "Effective mentoring frameworks: pair programming, code review as teaching, goal-setting, and feedback delivery. Grow your team's skill floor.", "Leadership mentor coach", "WAAO Learning", 70, 120),
			("Onboarding New Engineers", "Design a 30/60/90-day onboarding plan. First-week checklist, codebase orientation, pairing schedule, and psychological safety.", "Leadership onboard guide", "WAAO Learning", 60, 100),
		];

		foreach (var (title, desc, cat, provider, duration, xp) in courses)
		{
			db.Courses.Add(new Course
			{
				Id = Guid.CreateVersion7(),
				Title = title,
				Description = desc,
				Category = cat,
				Provider = provider,
				DurationMinutes = duration,
				SuggestedXp = xp,
				IsPublished = true,
				CreatedById = author.Id,
			});
		}

		await db.SaveChangesAsync(ct);
	}

	// ---------- Git curriculum (20 published courses, basic -> advanced, all under DevOps axis) ----------
	private static async Task SeedGitCoursesAsync(WaaoDbContext db, CancellationToken ct)
	{
		// Sentinel: only seed if no Git curriculum course exists yet.
		if (await db.Courses.AnyAsync(c => c.Title.StartsWith("Git 101"), ct)) return;

		var author = await db.Collaborators.FirstOrDefaultAsync(c => c.Email == "higor@waao.com.br", ct);
		if (author is null) return;

		// Category contains "devops" so the SkillRadar matcher routes XP to the DevOps axis.
		const string cat = "DevOps · Git";
		const string provider = "WAAO Learning";

		(string Title, string Description, int Duration, int SuggestedXp)[] courses =
		[
			// ---- Basics ----
			("Git 101: What is Version Control?",
				"Why version control exists, how Git differs from older centralized systems (CVS, SVN), the snapshot model, and the three states (working tree, index, repo). No commands yet — just the mental model.",
				40, 60),
			("Git 101: First-Time Setup",
				"Install Git, configure user.name and user.email per machine, choose a default branch name, set up your preferred editor and pager, and enable color output. Includes WAAO conventions (always commit as higor@waao.com.br).",
				30, 60),
			("Git 101: init, add, commit",
				"Create your first repository, stage files with git add, write your first commit, and inspect history with git log. Learn the difference between staging and committing.",
				45, 80),
			("Git 101: status, log, diff",
				"The three daily-driver verbs. Read git status output like a pro, navigate history with git log (--oneline, --graph, --stat), and compare working tree vs index vs HEAD with git diff.",
				50, 100),
			("Git 101: Remotes, clone, fetch, pull, push",
				"How distributed repositories actually work. Clone an existing repo, add a remote, fetch updates, pull (= fetch + merge), and push your changes back. Demystifies origin/main vs main.",
				60, 100),
			(".gitignore Essentials",
				"Pattern syntax (globs, negations, directories), where to put .gitignore (repo vs global ~/.gitignore_global), common WAAO patterns (.env, bin/, obj/, node_modules/), and how to untrack files already committed by mistake.",
				35, 70),

			// ---- Branching ----
			("Branches: create, switch, merge",
				"Why feature branches matter, creating branches with git switch -c, listing with git branch, deleting with -d / -D, and merging branches back into main. Includes the fast-forward vs no-ff distinction.",
				60, 120),
			("Merge vs Rebase: The Mental Model",
				"When to merge and when to rebase. The golden rule (don't rebase shared branches), pros/cons of each, what git log --graph looks like with each strategy, and how to choose for WAAO's trunk-based workflow.",
				70, 150),
			("Resolving Merge Conflicts",
				"What conflicts actually look like in files (<<<<<<, ======, >>>>>>), strategies to resolve (manual, --ours, --theirs), conflict markers in binaries (LFS), and using a proper merge tool (VS Code, Rider, vimdiff).",
				60, 120),
			("Pull Request Workflow",
				"Feature branch → push → open PR → code review → merge. Branch naming conventions (feat/, fix/), GitHub PR templates, draft PRs, requesting reviewers, and clean merge strategies (merge commit vs squash vs rebase-merge).",
				55, 110),

			// ---- Intermediate ----
			("Stash & WIP Workflows",
				"Save uncommitted work with git stash, list/apply/pop/drop stashes, stash with --include-untracked, stash named subsets with --keep-index, and recover lost stashes from the reflog.",
				50, 100),
			("Tags & Releases",
				"Lightweight vs annotated tags, semantic versioning (vMAJOR.MINOR.PATCH), pushing tags with git push --tags, deleting tags locally and remotely, and wiring tag pushes to a release pipeline in CI/CD.",
				45, 90),
			("Remote Tracking & Upstream",
				"git branch -vv to see tracking info, set upstream with push -u, switching to a remote branch with fetch + switch, pruning stale remote refs with fetch --prune.",
				40, 80),
			("Diffing Like a Pro",
				"Beyond git diff: log -p, log --stat, blame (line-by-line authorship), show (inspect a commit), word-diff for prose, and using git range-diff to compare two patch series.",
				55, 110),
			(".gitattributes, EOL & LFS",
				"Line-ending normalization (text=auto, eol=lf), language hints for diff (driver=csharp), filters, and Git LFS for binary assets. Avoiding the CRLF/LF nightmare across macOS, Linux, and Windows.",
				50, 100),

			// ---- Advanced ----
			("Interactive Rebase: Squash, Fixup, Reword, Drop",
				"git rebase -i HEAD~N for cleaning up a feature branch before merge. Reorder commits, squash WIP fixups, reword messages, drop accidental commits. Includes autosquash (--autosquash + fixup!).",
				75, 180),
			("Cherry-pick, Patch, and Apply",
				"Move a single commit between branches with cherry-pick, generate portable patches with format-patch, apply with apply or am, and resolve cherry-pick conflicts cleanly.",
				60, 140),
			("Reflog: Recovery & Time Travel",
				"git reflog is your undo log for everything: lost commits, deleted branches, botched rebases. Recover from --hard reset, find detached HEADs, and understand reflog expiration.",
				55, 130),
			("Bisect: Finding the Bad Commit",
				"Binary-search a regression with git bisect start/good/bad. Automate with bisect run + a test script. Real example: tracking down which commit broke a WAAO API endpoint.",
				60, 150),

			// ---- Expert ----
			("Worktrees & Parallel Branches",
				"Multiple working copies of one repo with git worktree add. When to use vs separate clones, integrating with editors, and the .worktrees/ convention used by WAAO tooling.",
				50, 130),
			("Submodules vs Subtrees vs Monorepo",
				"Three ways to share code across repos. When submodules make sense (independent versioning), when subtrees are simpler (vendored deps), and when a monorepo wins. Practical migration patterns.",
				70, 180),
			("Custom Hooks: pre-commit, pre-push, commit-msg",
				"Local hooks under .git/hooks, sharing hooks via Husky or pre-commit framework, common checks (lint, format, secrets, test). The WAAO pre-commit setup.",
				55, 130),
			("Signed Commits with GPG / SSH",
				"Configure commit signing (gpg.format=ssh + user.signingkey), --gpg-sign automatic signing, GitHub verified badge, and rotating keys. Why signed commits matter for supply-chain trust.",
				50, 120),
			("Git Internals: Objects, Refs, Packfiles",
				"What .git/ actually contains: blobs, trees, commits, tags. SHA addressing, the object database, refs, the index, and packfiles. Read porcelain through plumbing (cat-file, hash-object, ls-tree).",
				80, 220),
			("Rewriting History with git-filter-repo",
				"Remove secrets from history (the safer replacement for filter-branch), split out a directory into its own repo, mass-rewrite author emails, and force-push consequences. Always work on a fresh clone.",
				70, 200),
		];

		foreach (var (title, desc, duration, xp) in courses)
		{
			db.Courses.Add(new Course
			{
				Id = Guid.CreateVersion7(),
				Title = title,
				Description = desc,
				Category = cat,
				Provider = provider,
				DurationMinutes = duration,
				SuggestedXp = xp,
				IsPublished = true,
				CreatedById = author.Id,
			});
		}

		await db.SaveChangesAsync(ct);
	}

	// ---------- Challenges (20 published, 5 questions each, spread across 6 axes) ----------
	private static async Task SeedDefaultChallengesAsync(WaaoDbContext db, CancellationToken ct)
	{
		if (await db.Challenges.AnyAsync(ct)) return;

		var author = await db.Collaborators.FirstOrDefaultAsync(c => c.Email == "higor@waao.com.br", ct);
		if (author is null) return;

		var challenges = new List<(string Title, string Description, string Category, int SuggestedXp, int PassPercent, (string Prompt, string A, string B, string C, string D, char Correct)[] Questions)>
		{
			// Backend
			(
				"ASP.NET Core 9 Fundamentals",
				"Test your knowledge of ASP.NET Core 9 backend essentials: routing, middleware, dependency injection, and controllers.",
				"Backend api",
				150, 70,
				[
					("Which lifetime should be used for services that hold per-request state in ASP.NET Core?", "Singleton", "Scoped", "Transient", "Static", 'B'),
					("What attribute marks a controller class in ASP.NET Core?", "[Controller]", "[HttpController]", "[ApiController]", "[RouteController]", 'C'),
					("Which method registers a route in the middleware pipeline in .NET 9?", "app.UseRouting()", "app.MapControllers()", "app.AddControllers()", "app.RunControllers()", 'B'),
					("What does the [FromBody] attribute indicate in an action method?", "The parameter comes from the route.", "The parameter comes from the query string.", "The parameter is deserialized from the request body.", "The parameter is an optional header.", 'C'),
					("Which NuGet package provides Npgsql EF Core integration?", "Npgsql.Data", "Npgsql.EntityFrameworkCore.PostgreSQL", "Microsoft.EntityFrameworkCore.Npgsql", "PostgreSQL.EFCore", 'B'),
				]
			),
			(
				"Entity Framework Core & Migrations",
				"Validate your EF Core knowledge: migrations, query filters, configurations, and database patterns used in WAAO services.",
				"Backend database",
				180, 70,
				[
					("Which EF Core command creates a new migration?", "dotnet ef migrate add", "dotnet ef migrations add", "dotnet ef database migrate", "dotnet ef create migration", 'B'),
					("What does HasQueryFilter do in EF Core?", "Adds a database check constraint.", "Applies a global WHERE clause to all queries for that entity.", "Configures the primary key filter.", "Sets the default value for a column.", 'B'),
					("Which naming convention does WAAO use for database columns?", "PascalCase", "camelCase", "UPPER_SNAKE_CASE", "snake_case", 'D'),
					("How should you generate IDs for new entities in the WAAO codebase?", "Guid.NewGuid()", "Guid.CreateVersion7()", "new Guid()", "Guid.Empty", 'B'),
					("What happens when you call HasConversion<string>() on an enum property?", "The enum is stored as its numeric ordinal.", "The enum is stored as its string name.", "The enum is ignored by EF Core.", "The enum column becomes nullable.", 'B'),
				]
			),
			(
				"C# Language Features",
				"Test knowledge of modern C# features used across the WAAO backend: records, primary constructors, collection expressions, and null handling.",
				"Backend infra",
				120, 70,
				[
					("Which keyword creates an immutable reference type with value equality in C#?", "struct", "class", "record", "interface", 'C'),
					("What does the ?? operator do in C#?", "Checks if both operands are null.", "Returns the left operand if non-null; otherwise returns the right operand.", "Throws if the left operand is null.", "Converts null to an empty string.", 'B'),
					("What is the C# 13 collection expression syntax for an empty list?", "new List<T>()", "new T[] {}", "[]", "List.Empty<T>()", 'C'),
					("Which of these correctly uses a primary constructor in C#?", "public class Svc { public Svc(IRepo r) { _repo = r; } }", "public class Svc(IRepo Repository) { }", "public class Svc { public Svc(IRepo r) => _r = r; }", "public class Svc : IRepo { }", 'B'),
					("What does the async suffix convention mean in WAAO service methods?", "The method always returns void.", "The method returns Task or Task<T> and must be awaited.", "The method runs on a background thread.", "The method is exempt from cancellation token passing.", 'B'),
				]
			),
			(
				"Clean Architecture Concepts",
				"Assess your understanding of clean architecture, SOLID principles, and the layering pattern used in WAAO backend projects.",
				"Backend service",
				150, 70,
				[
					("In clean architecture, which layer should NOT depend on Entity Framework Core?", "Infra.EF", "Services", "Domain.Models", "API", 'C'),
					("Which SOLID principle states that a class should have only one reason to change?", "Open/Closed Principle", "Single Responsibility Principle", "Liskov Substitution Principle", "Interface Segregation Principle", 'B'),
					("Where should business logic live in the WAAO architecture?", "Controllers", "Services layer", "Domain.Models entities", "EF Core configurations", 'B'),
					("What is the role of an interface in dependency injection?", "To provide a concrete implementation.", "To act as an abstraction that allows swapping implementations.", "To define database schema.", "To enforce HTTP route constraints.", 'B'),
					("In WAAO, cross-module communication should use which mechanism?", "Direct DbContext cross-access", "HTTP clients or domain events", "Shared static classes", "In-memory caches", 'B'),
				]
			),

			// Frontend
			(
				"React 19 Core Concepts",
				"Test your React 19 knowledge: hooks, rendering, state management, and the new compiler features.",
				"Frontend React",
				150, 70,
				[
					("Which hook manages server state and caching in the WAAO frontend?", "useEffect", "useQuery from @tanstack/react-query", "useReducer", "useSWR", 'B'),
					("What is the preferred way to handle forms in WAAO frontend pages?", "Controlled inputs with useState", "Uncontrolled refs", "react-hook-form + zod schema", "FormData API directly", 'C'),
					("Which import alias maps to src/ in the WAAO frontend?", "@waao/", "@/", "~/", "src/", 'B'),
					("Which library provides Radix-based components in WAAO?", "@medtrack/ui", "@radix-ui/react", "shadcn/ui", "The project's own UI package", 'D'),
					("What does the use() hook in React 19 allow?", "Registering side effects after render.", "Reading a promise or context directly in render.", "Creating global state.", "Subscribing to browser events.", 'B'),
				]
			),
			(
				"TypeScript Strict Mode",
				"Validate your TypeScript knowledge: strict types, generics, type narrowing, and avoiding any in a real frontend codebase.",
				"Frontend TypeScript",
				120, 70,
				[
					("Which TypeScript compiler option enables all strict checks at once?", "\"noImplicitAny\": true", "\"strict\": true", "\"strictAll\": true", "\"noAny\": true", 'B'),
					("How do you type an array of strings in TypeScript?", "Array(string)", "string[]", "String[]", "Array<string>", 'B'),
					("What does the as const assertion do?", "Casts the value to a const type, widening all inferred types.", "Makes all properties and values readonly literal types.", "Disables strict mode for that expression.", "Converts the object to a frozen JavaScript object.", 'B'),
					("Which utility type makes all properties of T optional?", "Readonly<T>", "Required<T>", "Partial<T>", "NonNullable<T>", 'C'),
					("How do you narrow a union type Animal | null to Animal only?", "as Animal", "if (animal !== null)", "!animal", "animal as NotNull", 'B'),
				]
			),
			(
				"TanStack Query & Data Fetching",
				"Test your knowledge of server-state management with TanStack Query v5 in a React 19 WAAO frontend.",
				"Frontend ui data",
				130, 70,
				[
					("What is the primary benefit of TanStack Query over useEffect+fetch?", "It reduces bundle size.", "It provides automatic caching, background refetching, and deduplication.", "It replaces React state entirely.", "It works without a backend API.", 'B'),
					("Which TanStack Query hook is used for data mutations?", "useQuery", "useSuspenseQuery", "useMutation", "useInfiniteQuery", 'C'),
					("What does the queryKey array define in useQuery?", "The HTTP method to use.", "The unique cache key for this query's data.", "The list of query parameters.", "The request timeout.", 'B'),
					("How do you manually invalidate a query after a mutation?", "queryClient.resetQueries()", "queryClient.invalidateQueries({ queryKey: [...] })", "queryClient.clearCache()", "queryClient.refetchAll()", 'B'),
					("Which option controls how often a query refetches in the background?", "refetchInterval", "backgroundFetch", "pollingMs", "autoRefetch", 'A'),
				]
			),

			// DevOps
			(
				"Fly.io Deployment Fundamentals",
				"Test knowledge of deploying and operating .NET services on Fly.io: fly.toml, secrets, health checks, and rolling deploys.",
				"DevOps deploy cloud",
				150, 70,
				[
					("Which file configures a Fly.io application?", "docker-compose.yml", "fly.toml", "app.yaml", ".flyrc", 'B'),
					("How do you set a secret environment variable on Fly.io?", "fly env set KEY=value", "fly secrets set KEY=value", "fly config set KEY=value", "fly vars set KEY=value", 'B'),
					("What does fly machine start do?", "Creates a new VM.", "Starts a stopped machine.", "Deploys the application.", "Restarts all running machines.", 'B'),
					("Which Fly.io feature automatically stops idle machines to save cost?", "Auto-stop with min machines = 0", "Fly Autoscaler", "Machine hibernation policy", "Spot instance mode", 'A'),
					("Where does a .NET 9 Fly.io API typically read its DATABASE_URL?", "From the fly.toml [vars] section only.", "From environment variables injected by Fly at runtime.", "From appsettings.json.", "From a Kubernetes ConfigMap.", 'B'),
				]
			),
			(
				"GitHub Actions CI/CD",
				"Validate your understanding of GitHub Actions pipelines: triggers, jobs, caching, secrets, and deployment steps.",
				"DevOps ci/cd pipeline",
				130, 70,
				[
					("Which YAML key defines what events trigger a GitHub Actions workflow?", "triggers:", "on:", "events:", "when:", 'B'),
					("How do you reference a secret named MY_KEY in a workflow step?", "$MY_KEY", "${{ secrets.MY_KEY }}", "env.MY_KEY", "${MY_KEY}", 'B'),
					("Which step caches NuGet packages between runs?", "actions/cache with path ~/.nuget", "actions/nuget-cache", "dotnet/cache-packages", "actions/setup-dotnet --cache", 'A'),
					("What does the needs: key do in a job definition?", "Specifies which runner to use.", "Declares job dependencies, so this job runs after the listed jobs.", "Lists required secrets.", "Sets environment variables for the job.", 'B'),
					("Which GitHub Actions trigger runs a workflow on every push to main?", "on: push: branches: [main]", "on: branch: main", "on: commit: main", "on: deploy: production", 'A'),
				]
			),
			(
				"Docker for Backend Engineers",
				"Test knowledge of Docker essentials: Dockerfile patterns, multi-stage builds, and container best practices for .NET APIs.",
				"DevOps docker infra",
				120, 70,
				[
					("What does a multi-stage Dockerfile achieve for .NET apps?", "It runs the app on multiple OS platforms.", "It produces a smaller final image by separating build from runtime.", "It enables parallel container builds.", "It replaces docker-compose.", 'B'),
					("Which instruction sets the working directory inside a Docker container?", "CD /app", "WORKDIR /app", "ENV PATH=/app", "RUN cd /app", 'B'),
					("What is the purpose of .dockerignore?", "Defines which files to COPY into the image.", "Excludes files from the Docker build context to speed up builds.", "Lists images to pull before building.", "Configures network access for the container.", 'B'),
					("Which Dockerfile instruction should appear last to define the container start command?", "RUN", "CMD or ENTRYPOINT", "ENV", "EXPOSE", 'B'),
					("How do you pass environment variables to a running container?", "docker run --vars KEY=VALUE", "docker run -e KEY=VALUE", "docker run --env-file only", "docker env set KEY=VALUE", 'B'),
				]
			),

			// Quality
			(
				"Unit Testing with xUnit",
				"Test your knowledge of unit testing in .NET: xUnit patterns, assertions, mocking, and test organization best practices.",
				"Quality test coverage",
				120, 70,
				[
					("Which attribute marks a parameterless xUnit test method?", "[Test]", "[TestMethod]", "[Fact]", "[Scenario]", 'C'),
					("Which attribute runs an xUnit test with multiple data sets?", "[DataRow]", "[TestCase]", "[Theory] + [InlineData]", "[ParameterizedTest]", 'C'),
					("What does FluentAssertions' .Should().Be() check?", "Reference equality only.", "Value equality using the expected value.", "That the object is not null.", "Type compatibility.", 'B'),
					("Which EF Core provider is suitable for fast in-memory unit tests?", "Npgsql", "SqlServer InMemory", "EF Core InMemory provider", "Testcontainers Postgres", 'C'),
					("What is the recommended pattern for isolating service dependencies in unit tests?", "Use the real database.", "Mock all dependencies with Moq or NSubstitute.", "Skip dependency injection entirely.", "Use static helper classes.", 'B'),
				]
			),
			(
				"Code Review Excellence",
				"Assess your code review skills: what to look for, feedback quality, checklists, and review anti-patterns.",
				"Quality review",
				100, 70,
				[
					("What is the primary goal of a code review?", "Proving the reviewer is smarter.", "Finding bugs and improving code quality collaboratively.", "Enforcing a single coding style mechanically.", "Delaying merges until perfect.", 'B'),
					("Which of these is a review anti-pattern?", "Asking clarifying questions.", "Approving without reading.", "Suggesting alternative approaches.", "Pointing out security issues.", 'B'),
					("What should a reviewer do when unsure if a change is correct?", "Reject it immediately.", "Ask a clarifying question or request a test.", "Approve to unblock the author.", "Ignore the section.", 'B'),
					("Which aspect is most important to check in a security-sensitive PR?", "Variable naming consistency.", "Correct authorization checks and input validation.", "Comment formatting.", "File organization.", 'B'),
					("What makes feedback in a code review most effective?", "Being as brief as possible.", "Explaining the why, not just the what, with suggestions.", "Always blocking the merge.", "Commenting on every line.", 'B'),
				]
			),

			// Communication
			(
				"Technical Documentation",
				"Validate your technical writing skills: ADRs, runbooks, README quality, and docs-as-code practices.",
				"Communication docs write",
				100, 70,
				[
					("What is an Architecture Decision Record (ADR)?", "A log of all bug fixes.", "A document capturing the context, decision, and consequences of a significant architectural choice.", "A diagram of the system.", "A sprint planning artifact.", 'B'),
					("What makes a runbook effective?", "It's written once and never updated.", "It contains numbered steps that anyone can follow, including rollback procedures.", "It describes only the happy path.", "It requires deep system knowledge to execute.", 'B'),
					("Which Markdown feature best organizes a multi-section README?", "Horizontal rules", "Numbered lists only", "Heading hierarchy (## and ###) with a table of contents", "Bold text for every heading", 'C'),
					("What does docs-as-code mean?", "Writing docs inside the source code files.", "Treating documentation with the same tooling, reviews, and versioning as application code.", "Auto-generating docs from comments only.", "Storing docs in a separate wiki tool.", 'B'),
					("What should the TLDR or Summary section of a PR description contain?", "A detailed technical explanation of every change.", "A one or two sentence summary of what changed and why.", "The full commit log.", "Links to external references only.", 'B'),
				]
			),
			(
				"Effective Presentation Skills",
				"Test your ability to structure and deliver clear technical presentations and demos to mixed audiences.",
				"Communication present talk",
				80, 70,
				[
					("What is the most important thing to establish at the start of a technical presentation?", "List all technical details upfront.", "State the goal: what the audience will understand or be able to do after.", "Show your agenda slide for 10 minutes.", "Apologize for any live demo risks.", 'B'),
					("How should you handle a question you don't know the answer to?", "Make up a plausible answer.", "Acknowledge you don't know and commit to following up.", "Change the subject quickly.", "Ask the audience to figure it out.", 'B'),
					("Which technique helps non-technical stakeholders understand complex systems?", "Present raw source code.", "Use analogies and diagrams, avoiding jargon.", "Read from detailed API documentation.", "Skip context and go straight to implementation.", 'B'),
					("What is the purpose of a live demo in a technical presentation?", "To prove the code compiles.", "To show real, working functionality and build credibility with the audience.", "To fill time if slides run short.", "To replace slide content entirely.", 'B'),
					("How many key takeaways should a focused technical presentation aim for?", "10 or more", "As many as fit on one slide", "3 to 5 clear points", "None — just show code", 'C'),
				]
			),

			// Leadership
			(
				"Engineering Leadership Basics",
				"Test your understanding of technical leadership: 1:1s, delegation, unblocking teams, and giving direction without micromanaging.",
				"Leadership lead guide",
				150, 70,
				[
					("What is the primary purpose of a 1:1 meeting between a lead and a team member?", "Status reporting to management.", "Building trust, surfacing blockers, and coaching the team member.", "Code review walkthrough.", "Sprint planning.", 'B'),
					("What does effective technical delegation involve?", "Assigning tasks with full implementation prescriptions.", "Communicating the desired outcome and constraints, then trusting the engineer.", "Doing the work yourself to be sure.", "Delegating only tasks you don't want to do.", 'B'),
					("When a team member is blocked, what should a lead do first?", "Take over the work.", "Ask clarifying questions to understand the blocker before acting.", "Escalate to management immediately.", "Ignore it and check in the next sprint.", 'B'),
					("What distinguishes a strong technical direction from micromanagement?", "Telling engineers exactly which files to edit.", "Defining goals and guardrails while giving engineers autonomy over implementation.", "Approving every commit personally.", "Requiring daily detailed updates.", 'B'),
					("What is a key sign that a team has healthy psychological safety?", "No one ever disagrees in meetings.", "Team members freely raise concerns, mistakes, and ideas without fear of blame.", "All decisions are escalated to the lead.", "Only senior engineers speak in meetings.", 'B'),
				]
			),
			(
				"Mentoring & Coaching Developers",
				"Validate your knowledge of mentoring techniques: pair programming, feedback delivery, goal-setting, and growing junior engineers.",
				"Leadership mentor coach",
				120, 70,
				[
					("What is the difference between mentoring and coaching?", "They are identical.", "Mentoring shares experience and advice; coaching uses questions to unlock the person's own thinking.", "Coaching is only for underperformers.", "Mentoring only happens during onboarding.", 'B'),
					("Which pair programming style has the junior engineer drive (type) while the senior navigates?", "Ping-pong pairing", "Strong-style pairing", "Mob programming", "Shadow pairing", 'B'),
					("What makes feedback most effective for a junior developer?", "Delivering it publicly for accountability.", "Making it specific, behavioral, and tied to impact — with a suggested alternative.", "Waiting until a formal review cycle.", "Focusing only on what went wrong.", 'B'),
					("How should a mentor set goals with a mentee?", "Assign pre-written goals from a template.", "Collaboratively define SMART goals aligned to the mentee's interests and team needs.", "Set only short-term goals.", "Avoid goals to keep pressure low.", 'B'),
					("What is the best way to teach a junior engineer to debug independently?", "Fix the bug for them.", "Pair with them and ask guiding questions instead of giving answers directly.", "Send links to documentation.", "Leave them alone until they figure it out.", 'B'),
				]
			),
			(
				"Onboarding New Team Members",
				"Test your knowledge of designing effective onboarding programs: first-week plans, codebase walkthroughs, and psychological safety.",
				"Leadership onboard guide",
				100, 70,
				[
					("What should a new engineer accomplish in their first week?", "Ship a major feature to production.", "Complete environment setup, read key docs, and make a small meaningful first contribution.", "Attend all company meetings.", "Review the entire codebase.", 'B'),
					("Which element most contributes to a new engineer feeling psychologically safe?", "A long list of rules to follow.", "Leaders who share their own mistakes and encourage questions.", "Strict performance targets from day one.", "A desk near the senior engineers.", 'B'),
					("What is a 30/60/90-day plan?", "A sprint planning tool.", "A structured onboarding roadmap with checkpoints at one, two, and three months.", "A performance improvement plan.", "A quarterly OKR framework.", 'B'),
					("Why is pair programming valuable during onboarding?", "It replaces code review.", "It transfers tacit knowledge about codebase patterns faster than docs alone.", "It ensures the new engineer writes no bugs.", "It satisfies compliance requirements.", 'B'),
					("What is the most important piece of documentation for a new backend engineer joining WAAO?", "The company org chart.", "The CLAUDE.md / README plus the architecture and pattern docs.", "The full commit history.", "The team's Slack channel history.", 'B'),
				]
			),
			// Two more for Backend and Frontend to reach 20 total
			(
				"PostgreSQL Advanced Patterns",
				"Deep-dive into PostgreSQL features relevant to the WAAO backend: partial indexes, advisory locks, and pg_stat_statements.",
				"Backend sql infra",
				180, 70,
				[
					("What is a partial index in PostgreSQL?", "An index on a subset of columns.", "An index with a WHERE clause that only covers rows matching that condition.", "An index used only for partial text search.", "A non-unique index.", 'B'),
					("What does pg_advisory_lock do?", "Locks a specific table row.", "Acquires an application-level advisory lock using a key, without locking database objects.", "Freezes a transaction.", "Prevents vacuuming.", 'B'),
					("Which PostgreSQL view shows cumulative query statistics?", "pg_stat_tables", "pg_stat_statements", "pg_query_log", "pg_activity", 'B'),
					("What is the safest way to add a NOT NULL column to a large production table?", "ALTER TABLE ADD COLUMN col NOT NULL", "Add nullable column, backfill data, then add NOT NULL constraint", "DROP and re-create the table", "Use a trigger instead", 'B'),
					("Which isolation level prevents phantom reads in PostgreSQL?", "READ COMMITTED", "REPEATABLE READ", "SERIALIZABLE", "READ UNCOMMITTED", 'C'),
				]
			),
			(
				"Cloudflare Workers for Frontend Deployment",
				"Test your knowledge of deploying React frontends as Cloudflare Workers: wrangler, KV, R2, and routing.",
				"Frontend DevOps cloud deploy",
				130, 70,
				[
					("Which tool deploys a Cloudflare Worker from the command line?", "fly deploy", "wrangler deploy", "cf-cli push", "cloudflare publish", 'B'),
					("What is Cloudflare KV used for?", "Running SQL queries at the edge.", "Storing key-value pairs globally accessible to Workers.", "Hosting static files.", "Managing DNS records.", 'B'),
					("How does a Cloudflare Pages deployment trigger automatically in WAAO?", "Via fly deploy on every commit.", "Git-connected deployment triggers on push to main.", "A cron job runs wrangler publish nightly.", "Manually via the dashboard.", 'B'),
					("What is R2 in the Cloudflare ecosystem?", "A routing algorithm.", "S3-compatible object storage with no egress fees.", "A Redis-compatible KV store.", "A CDN caching layer.", 'B'),
					("Which wrangler.toml field sets the entry point Worker script?", "main", "entry", "src", "script", 'A'),
				]
			),
		};

		foreach (var (title, desc, cat, xp, pass, questions) in challenges)
		{
			var challenge = new Challenge
			{
				Id = Guid.CreateVersion7(),
				Title = title,
				Description = desc,
				Category = cat,
				SuggestedXp = xp,
				PassPercent = pass,
				IsPublished = true,
				CreatedById = author.Id,
			};
			db.Challenges.Add(challenge);

			for (var i = 0; i < questions.Length; i++)
			{
				var (prompt, a, b, c, d, correct) = questions[i];
				db.ChallengeQuestions.Add(new ChallengeQuestion
				{
					Id = Guid.CreateVersion7(),
					ChallengeId = challenge.Id,
					Order = i + 1,
					Prompt = prompt,
					OptionA = a,
					OptionB = b,
					OptionC = c,
					OptionD = d,
					CorrectOption = correct,
				});
			}
		}

		await db.SaveChangesAsync(ct);
	}
}
