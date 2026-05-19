# Manual XP Economy + Clean Slate + Single Admin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all XP admin-granted only (no automatic XP anywhere), reset every collaborator to level 0 / 0 XP, clear the XP ledger, and leave `higor@waao.com.br` as the sole seeded Admin.

**Architecture:** Keep `GamificationEngine.RecordAsync` as the single XP write path but call it only from a new admin grant endpoint. Strip the XP-award calls out of `BadgeEvaluator`, `StreakTracker`, and `CareerEventService` (badges/streaks still run, 0 XP). **Folded in from the onboarding spec:** `BadgeEvaluator`/`StreakTracker` also fail-closed gate on `Collaborator.OnboardingCompletedAt` (no badges/streaks until onboarded); the column + backfill + bootstrap-admin onboarded-seed ride along in the entity edit and reset migration. Clamp levels to 0 below the first threshold. A destructive EF migration resets balances, hard-clears `xp_transactions`, soft-deletes non-higor collaborators, and backfills existing rows as onboarded. Frontend gains an admin "Grant XP" control and drops auto XP/level-up celebrations. (The onboarding **wizard UI + status/complete endpoints** are a separate Feature D plan, run after this.)

**Tech Stack:** .NET 9, EF Core 9 + Npgsql (snake_case), FluentValidation, xUnit + FluentAssertions + EF InMemory (new test project), React 19 + TypeScript + axios + i18next.

**Spec:** `docs/superpowers/specs/2026-05-19-manual-xp-economy-design.md`

**Conventions (from repo standards):** TABS indentation, file-scoped namespaces, primary-constructor DI with PascalCase params, DTOs as `record`, FluentValidation (no data annotations), `GuidGenerator`/`Guid.CreateVersion7()`, `DateTime.UtcNow`, soft delete only (the single approved exception: hard-clearing `xp_transactions`). Conventional commits, **no AI mentions**. Commit author email: `higor@waao.com.br`.

---

### Task 1: Create the test project

**Files:**
- Create: `tests/Waao.Tests/Waao.Tests.csproj`
- Create: `tests/Waao.Tests/Support/TestDb.cs`
- Create: `tests/Waao.Tests/SanityTests.cs`
- Modify: `Waao.sln`

- [ ] **Step 1: Create the test project file**

`tests/Waao.Tests/Waao.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<TargetFramework>net9.0</TargetFramework>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<IsPackable>false</IsPackable>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
		<PackageReference Include="xunit" Version="2.9.2" />
		<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
		<PackageReference Include="FluentAssertions" Version="6.12.1" />
		<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
	</ItemGroup>
	<ItemGroup>
		<ProjectReference Include="..\..\src\Waao.Services\Waao.Services.csproj" />
		<ProjectReference Include="..\..\src\Waao.Infra.EF\Waao.Infra.EF.csproj" />
		<ProjectReference Include="..\..\src\Waao.Domain.Models\Waao.Domain.Models.csproj" />
		<ProjectReference Include="..\..\src\Waao.Services.Abstractions\Waao.Services.Abstractions.csproj" />
	</ItemGroup>
</Project>
```

- [ ] **Step 2: Add a DbContext test helper**

`tests/Waao.Tests/Support/TestDb.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Waao.Infra.EF;

namespace Waao.Tests.Support;

public static class TestDb
{
	public static WaaoDbContext New()
	{
		var options = new DbContextOptionsBuilder<WaaoDbContext>()
			.UseInMemoryDatabase($"waao-{Guid.NewGuid()}")
			.Options;
		return new WaaoDbContext(options);
	}
}
```

- [ ] **Step 3: Add a sanity test**

`tests/Waao.Tests/SanityTests.cs`:
```csharp
using FluentAssertions;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests;

public class SanityTests
{
	[Fact]
	public void InMemoryContext_CanBeCreated()
	{
		using var db = TestDb.New();
		db.Should().NotBeNull();
	}
}
```

- [ ] **Step 4: Add the project to the solution**

Run: `dotnet sln Waao.sln add tests/Waao.Tests/Waao.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 5: Run the test**

Run: `dotnet test tests/Waao.Tests/Waao.Tests.csproj`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add tests/Waao.Tests Waao.sln
git -c user.email="higor@waao.com.br" commit -m "test: add Waao.Tests xUnit project"
```

---

### Task 2: Level-0 model (`CurrentLevel` default 0 + clamp `ComputeLevelAsync`)

**Files:**
- Modify: `src/Waao.Domain.Models/Entities/Collaborator.cs` (the `CurrentLevel` line)
- Modify: `src/Waao.Services/Gamification/GamificationEngine.cs:55-66` (`ComputeLevelAsync`)
- Test: `tests/Waao.Tests/Gamification/ComputeLevelTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Waao.Tests/Gamification/ComputeLevelTests.cs`:
```csharp
using FluentAssertions;
using Waao.Domain.Models.Entities;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class ComputeLevelTests
{
	[Fact]
	public async Task ZeroXp_IsLevel0()
	{
		using var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });
		await db.SaveChangesAsync();
		var engine = new GamificationEngine(db);

		(await engine.ComputeLevelAsync(0)).Should().Be(0);
		(await engine.ComputeLevelAsync(50)).Should().Be(0);
		(await engine.ComputeLevelAsync(100)).Should().Be(2);
	}

	[Fact]
	public async Task NoDefinitions_IsLevel0()
	{
		using var db = TestDb.New();
		var engine = new GamificationEngine(db);
		(await engine.ComputeLevelAsync(999)).Should().Be(0);
	}
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter ComputeLevelTests`
Expected: FAIL — current code returns 1 for 0 XP / fallback `(int)(totalXp/500)+1`.

- [ ] **Step 3: Change `ComputeLevelAsync` to clamp to 0**

In `src/Waao.Services/Gamification/GamificationEngine.cs`, replace the whole `ComputeLevelAsync` method body (currently lines 55-66) with:
```csharp
	public async Task<int> ComputeLevelAsync(long totalXp, CancellationToken ct = default)
	{
		var definitions = await Db.LevelDefinitions
			.OrderBy(l => l.Level)
			.ToListAsync(ct);

		// No XP (or no definitions) => unranked level 0. Everyone starts here;
		// only admin-granted XP moves a collaborator above 0.
		if (definitions.Count == 0)
			return 0;

		var lvl = 0;
		foreach (var def in definitions)
			if (totalXp >= def.XpThreshold) lvl = def.Level;
		return lvl;
	}
```

- [ ] **Step 4: Change the entity default + add onboarding field**

In `src/Waao.Domain.Models/Entities/Collaborator.cs`, change:
```csharp
	public int CurrentLevel { get; set; } = 1;
```
to:
```csharp
	public int CurrentLevel { get; set; } = 0;
```
Then, in the same `// ----- Auth -----` / gamification region, add the onboarding
field (folded in from the onboarding spec — null means not yet onboarded):
```csharp
	public DateTime? OnboardingCompletedAt { get; set; }
```

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/Waao.Tests --filter ComputeLevelTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services/Gamification/GamificationEngine.cs src/Waao.Domain.Models/Entities/Collaborator.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: level 0 model — clamp ComputeLevel, default CurrentLevel 0"
```

---

### Task 3: Badges unlock with 0 XP

**Files:**
- Modify: `src/Waao.Services/Gamification/BadgeEvaluator.cs:69-74` (remove the XP block)
- Test: `tests/Waao.Tests/Gamification/BadgeNoXpTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Waao.Tests/Gamification/BadgeNoXpTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class BadgeNoXpTests
{
	[Fact]
	public async Task FirstLoginBadge_Unlocks_ButGrantsNoXp()
	{
		using var db = TestDb.New();
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow), LastLoginAt = DateTime.UtcNow, OnboardingCompletedAt = DateTime.UtcNow };
		db.Collaborators.Add(c);
		db.Badges.Add(new Badge { Id = Guid.CreateVersion7(), Code = "FIRST_LOGIN", Name = "Hello WAAO", Category = BadgeCategory.Activity, Rarity = BadgeRarity.Common, XpReward = 50, UnlockRule = "First successful login" });
		await db.SaveChangesAsync();

		var engine = new GamificationEngine(db);
		var evaluator = new BadgeEvaluator(db, engine);
		var awarded = await evaluator.EvaluateAsync(c.Id);
		await db.SaveChangesAsync();

		awarded.Select(b => b.Code).Should().Contain("FIRST_LOGIN");
		(await db.XpTransactions.CountAsync()).Should().Be(0);
		(await db.Collaborators.FirstAsync()).TotalXp.Should().Be(0);
	}

	[Fact]
	public async Task NotOnboarded_NoBadgesUnlock()
	{
		using var db = TestDb.New();
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow), LastLoginAt = DateTime.UtcNow, OnboardingCompletedAt = null };
		db.Collaborators.Add(c);
		db.Badges.Add(new Badge { Id = Guid.CreateVersion7(), Code = "FIRST_LOGIN", Name = "Hello WAAO", Category = BadgeCategory.Activity, Rarity = BadgeRarity.Common, XpReward = 50, UnlockRule = "First successful login" });
		await db.SaveChangesAsync();

		var evaluator = new BadgeEvaluator(db, new GamificationEngine(db));
		var awarded = await evaluator.EvaluateAsync(c.Id);
		await db.SaveChangesAsync();

		awarded.Should().BeEmpty();
		(await db.CollaboratorBadges.CountAsync()).Should().Be(0);
	}
}
```

> If `FIRST_LOGIN`'s unlock rule needs more than `LastLoginAt`, adjust the seeded collaborator fields in the test so the rule fires; the assertion that matters is **0 XpTransactions / 0 TotalXp** for any unlocked badge.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter BadgeNoXpTests`
Expected: FAIL — `XpTransactions` count is 1 (badge awards 50 XP).

- [ ] **Step 3a: Add the onboarding gate (folded in from onboarding spec)**

At the very start of `EvaluateAsync` in `src/Waao.Services/Gamification/BadgeEvaluator.cs`, before any badge/context logic, add a fail-closed gate so no badge unlocks until onboarding is complete:
```csharp
		var onboarded = await Db.Collaborators
			.Where(c => c.Id == collaboratorId)
			.Select(c => c.OnboardingCompletedAt)
			.FirstOrDefaultAsync(ct);
		if (onboarded is null)
			return [];
```
(`using Microsoft.EntityFrameworkCore;` is already in the file. Returning `[]` matches the `IReadOnlyList<Badge>` return type.)

- [ ] **Step 3b: Remove the XP-award block**

In `src/Waao.Services/Gamification/BadgeEvaluator.cs`, delete exactly this block (currently right after the `Db.CollaboratorBadges.Add(...)` call):
```csharp
			if (badge.XpReward > 0)
			{
				await Gamification.RecordAsync(
					collaboratorId, badge.XpReward, XpSource.BadgeUnlock,
					$"Badge unlocked: {badge.Name}", badge.Id, nameof(Badge), ct);
			}
```
Leave `awards.Add(badge);` and the rest intact. If `Gamification` / `XpSource` become unused in the file, remove the now-unused `using` / constructor parameter **only if** nothing else in the file references them (search the file first; `BadgeEvaluator` likely keeps the `GamificationEngine Gamification` ctor param only for this — if so, drop it from the primary constructor and update `Program.cs` DI if it constructed it explicitly; it is `AddScoped<BadgeEvaluator>()` so DI auto-resolves — no Program.cs change needed).

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Waao.Tests --filter BadgeNoXpTests`
Expected: PASS.

- [ ] **Step 5: Build the solution**

Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services/Gamification/BadgeEvaluator.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: badges unlock without granting XP"
```

---

### Task 4: Streaks track with 0 XP

**Files:**
- Modify: `src/Waao.Services/Gamification/StreakTracker.cs:72-92` (`AwardThresholdBonus`)
- Test: `tests/Waao.Tests/Gamification/StreakNoXpTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Waao.Tests/Gamification/StreakNoXpTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Services.Gamification;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class StreakNoXpTests
{
	[Fact]
	public async Task CrossingStreakThreshold_AdvancesStreak_ButGrantsNoXp()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLoginStreakDays = 6,
			LastLoginDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
			OnboardingCompletedAt = DateTime.UtcNow,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var engine = new GamificationEngine(db);
		var tracker = new StreakTracker(db, engine);
		var (current, _, bonus) = await tracker.RegisterLoginAsync(c.Id);
		await db.SaveChangesAsync();

		current.Should().Be(7);          // streak still advances
		bonus.Should().Be(0);            // no XP bonus
		(await db.XpTransactions.CountAsync()).Should().Be(0);
		(await db.Collaborators.FirstAsync()).TotalXp.Should().Be(0);
	}

	[Fact]
	public async Task NotOnboarded_StreakDoesNotAdvance()
	{
		using var db = TestDb.New();
		var c = new Collaborator
		{
			Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			CurrentLoginStreakDays = 6,
			LastLoginDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
			OnboardingCompletedAt = null,
		};
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var tracker = new StreakTracker(db, new GamificationEngine(db));
		var (current, longest, bonus) = await tracker.RegisterLoginAsync(c.Id);
		await db.SaveChangesAsync();

		(current, longest, bonus).Should().Be((0, 0, 0));
		(await db.Collaborators.FirstAsync()).CurrentLoginStreakDays.Should().Be(6); // unchanged
	}
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter StreakNoXpTests`
Expected: FAIL — `bonus` > 0 and an `XpTransaction` is created at the 7-day threshold.

- [ ] **Step 3a: Add the onboarding gate (folded in from onboarding spec)**

In `src/Waao.Services/Gamification/StreakTracker.cs`, in **both** `RegisterActivityAsync` and `RegisterLoginAsync`, immediately after the existing `if (collaborator is null) return (0, 0, 0);` line, add:
```csharp
		if (collaborator.OnboardingCompletedAt is null) return (0, 0, 0);
```
This fail-closed gate means streaks do not advance and nothing is recorded until onboarding is complete.

- [ ] **Step 3b: Neutralize `AwardThresholdBonus`**

In `src/Waao.Services/Gamification/StreakTracker.cs`, replace the entire `AwardThresholdBonus` method (currently lines 72-92) with:
```csharp
	// Streaks are still tracked, but no longer grant XP — XP is admin-only.
	// Kept as a no-op so the call sites and return contract are unchanged.
	private static Task<int> AwardThresholdBonus(
		Guid collaboratorId, int prevStreak, int currentStreak, string label, CancellationToken ct)
		=> Task.FromResult(0);
```
Then update the two call sites (lines ~34 and ~55) — they currently `await AwardThresholdBonus(...)`. `await` on `Task<int>` still works, so **no call-site change is required**. Remove the now-unused `using Waao.Domain.Models.Enums;` and `using Waao.Domain.Models.Rules;` only if nothing else in the file uses them (search first — `XpSource`/`XpRules` were only used in the removed body). Leave the `GamificationEngine Gamification` primary-constructor param (DI registers `StreakTracker` via `AddScoped`; removing it is optional cleanup — only do it if no other member uses `Gamification`, and it requires no Program.cs change).

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Waao.Tests --filter StreakNoXpTests`
Expected: PASS.

- [ ] **Step 5: Build**

Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services/Gamification/StreakTracker.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: streaks tracked without granting XP"
```

---

### Task 5: Career events award 0 XP + remove dead engine method

**Files:**
- Modify: `src/Waao.Services/Services/CareerEventService.cs` (the XP call + result)
- Modify: `src/Waao.Services/Gamification/GamificationEngine.cs` (remove `AwardCareerEventXpAsync`)
- Test: `tests/Waao.Tests/Gamification/CareerEventNoXpTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Waao.Tests/Gamification/CareerEventNoXpTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Gamification;

public class CareerEventNoXpTests
{
	[Fact]
	public async Task CreatingCareerEvent_RecordsEvent_With0Xp_AndNoLevelChange()
	{
		using var db = TestDb.New();
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow), CurrentLevel = 0, OnboardingCompletedAt = DateTime.UtcNow };
		db.Collaborators.Add(c);
		await db.SaveChangesAsync();

		var engine = new GamificationEngine(db);
		var streaks = new StreakTracker(db, engine);
		var badges = new BadgeEvaluator(db, engine);
		var validator = new Waao.Services.Validation.CreateCareerEventValidator();
		var svc = new CareerEventService(db, engine, streaks, badges, validator);

		var result = await svc.CreateAsync(new CreateCareerEventDto
		{
			CollaboratorId = c.Id,
			Type = CareerEventType.Training,
			EventDate = DateOnly.FromDateTime(DateTime.UtcNow),
			Title = "Did a course",
		});

		result.XpAwarded.Should().Be(0);
		result.StreakBonusXp.Should().Be(0);
		result.LevelBefore.Should().Be(0);
		result.LevelAfter.Should().Be(0);
		(await db.XpTransactions.CountAsync()).Should().Be(0);
	}
}
```

> Verify `CareerEventService`'s constructor parameter order/types against the file and adjust the `new CareerEventService(...)` arguments to match (it uses primary-constructor DI: `WaaoDbContext`, `GamificationEngine`, `StreakTracker`, `BadgeEvaluator`, `IValidator<CreateCareerEventDto>`). Use the real validator type name from `src/Waao.Services/Validation/`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter CareerEventNoXpTests`
Expected: FAIL — `XpAwarded` > 0 for a Training event.

- [ ] **Step 3: Remove the career-event XP award**

In `src/Waao.Services/Services/CareerEventService.cs`, find:
```csharp
		// 1. XP for the event itself
		var eventXp = await Gamification.AwardCareerEventXpAsync(entity, ct);
```
Replace with:
```csharp
		// XP is admin-granted only — career events no longer auto-award XP.
		const int eventXp = 0;
		entity.XpAwarded = 0;
```
Leave the streak + badge + reload lines and the `CareerEventCreatedDto` block unchanged (`XpAwarded = eventXp` now resolves to 0; `LevelBefore`/`LevelAfter` stay equal because nothing grants XP).

- [ ] **Step 4: Remove the now-dead engine method**

In `src/Waao.Services/Gamification/GamificationEngine.cs`, delete the entire `AwardCareerEventXpAsync` method:
```csharp
	public async Task<int> AwardCareerEventXpAsync(CareerEvent evt, CancellationToken ct = default)
	{
		var amount = XpRules.XpForCareerEvent(evt.Type);
		if (amount <= 0) return 0;

		var reason = $"{evt.Type}: {evt.Title}";
		await RecordAsync(evt.CollaboratorId, amount, XpSource.CareerEvent, reason, evt.Id, nameof(CareerEvent), ct);
		evt.XpAwarded = amount;
		return amount;
	}
```
Keep `RecordAsync` and `ComputeLevelAsync`. If `XpRules.XpForCareerEvent` now has no callers, leave `XpRules` as-is (out of scope to delete other rules); do not chase unused code beyond this method. Remove a now-unused `using` only if the compiler flags it.

- [ ] **Step 5: Run tests + build**

Run: `dotnet test tests/Waao.Tests --filter CareerEventNoXpTests`
Expected: PASS.
Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services/Services/CareerEventService.cs src/Waao.Services/Gamification/GamificationEngine.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: career events no longer auto-award XP; drop dead engine method"
```

---

### Task 6: Admin manual XP grant endpoint

**Files:**
- Modify: `src/Waao.Services.Abstractions/Dtos/` (new `GrantXpDto` — add to an existing admin DTO file, e.g. `AdminDtos.cs`; create if no admin DTO file exists)
- Modify: `src/Waao.Services.Abstractions/Services/IServices.cs` (add `GrantXpAsync` to `IAdminService`)
- Modify: `src/Waao.Services/Services/AdminService.cs` (implement `GrantXpAsync`)
- Create: `src/Waao.Services/Validation/GrantXpValidator.cs`
- Modify: `src/Waao.API/Controllers/AdminController.cs` (add endpoint under the `// ----- People -----` group)
- Test: `tests/Waao.Tests/Admin/GrantXpTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Waao.Tests/Admin/GrantXpTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Gamification;
using Waao.Services.Services;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Admin;

public class GrantXpTests
{
	private static (AdminService svc, WaaoDbContext db, Collaborator c, Guid admin) Build()
	{
		var db = TestDb.New();
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 1, XpThreshold = 0, Title = "Newcomer" });
		db.LevelDefinitions.Add(new LevelDefinition { Id = Guid.CreateVersion7(), Level = 2, XpThreshold = 100, Title = "Rookie" });
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow), CurrentLevel = 0 };
		db.Collaborators.Add(c);
		db.SaveChanges();
		var svc = AdminServiceFactory.Create(db); // see Step 3 note
		return (svc, db, c, Guid.CreateVersion7());
	}

	[Fact]
	public async Task GrantXp_Positive_AddsXp_RecomputesLevel_WritesAdminTransaction()
	{
		var (svc, db, c, admin) = Build();
		var dto = await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = 120, Reason = "Q2 project" }, admin);

		dto.TotalXp.Should().Be(120);
		dto.CurrentLevel.Should().Be(2);
		var tx = await db.XpTransactions.SingleAsync();
		tx.Amount.Should().Be(120);
		tx.Source.Should().Be(XpSource.Admin);
		tx.Reason.Should().Be("Q2 project");
	}

	[Fact]
	public async Task GrantXp_Negative_Deducts()
	{
		var (svc, db, c, admin) = Build();
		await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = 100, Reason = "grant" }, admin);
		var dto = await svc.GrantXpAsync(c.Id, new GrantXpDto { Amount = -40, Reason = "correction" }, admin);
		dto.TotalXp.Should().Be(60);
	}

	[Fact]
	public async Task GrantXp_MissingCollaborator_Throws()
	{
		var (svc, _, _, admin) = Build();
		var act = async () => await svc.GrantXpAsync(Guid.NewGuid(), new GrantXpDto { Amount = 10, Reason = "x" }, admin);
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}
}
```

> **AdminServiceFactory note:** `AdminService` is constructed via primary-constructor DI. In `tests/Waao.Tests/Support/`, add `AdminServiceFactory.Create(WaaoDbContext db)` that news up `AdminService` with the same dependencies it declares (open `src/Waao.Services/Services/AdminService.cs` to read its primary-constructor parameter list, then pass a real `GamificationEngine(db)` and `WaaoDbContext`; supply trivial instances/stubs for any other params — most are `WaaoDbContext`-backed). Keep the factory minimal.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter GrantXpTests`
Expected: FAIL — `GrantXpDto` / `GrantXpAsync` do not exist (compile error).

- [ ] **Step 3: Add the DTO**

Create `src/Waao.Services.Abstractions/Dtos/AdminGrantDtos.cs` (or append to the existing admin DTO file if one exists — check `src/Waao.Services.Abstractions/Dtos/` first):
```csharp
namespace Waao.Services.Abstractions.Dtos;

public record GrantXpDto
{
	public int Amount { get; init; }
	public string Reason { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Add the validator**

`src/Waao.Services/Validation/GrantXpValidator.cs`:
```csharp
using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class GrantXpValidator : AbstractValidator<GrantXpDto>
{
	public GrantXpValidator()
	{
		RuleFor(x => x.Amount).NotEqual(0).WithMessage("Amount must be non-zero (negative allowed for corrections).");
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(280);
	}
}
```

- [ ] **Step 5: Add `GrantXpAsync` to the interface**

In `src/Waao.Services.Abstractions/Services/IServices.cs`, inside `interface IAdminService`, add (near `PromoteAsync` / `SetRoleKindAsync`):
```csharp
	Task<CollaboratorDto> GrantXpAsync(Guid collaboratorId, GrantXpDto dto, Guid adminId, CancellationToken ct = default);
```

- [ ] **Step 6: Implement in `AdminService`**

In `src/Waao.Services/Services/AdminService.cs`, add the method (match the file's existing dependency names — it has `WaaoDbContext Db`; ensure `GamificationEngine Gamification` and `IValidator<GrantXpDto> GrantXpValidator` are primary-constructor params, adding them if absent, and add the matching `using FluentValidation;` / mapper using). Use the existing collaborator→DTO mapper used elsewhere in the file (e.g. `CollaboratorMapper.ToDto`):
```csharp
	public async Task<CollaboratorDto> GrantXpAsync(Guid collaboratorId, GrantXpDto dto, Guid adminId, CancellationToken ct = default)
	{
		await GrantXpValidator.ValidateAndThrowAsync(dto, ct);

		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		await Gamification.RecordAsync(
			collaboratorId, dto.Amount, XpSource.Admin, dto.Reason,
			adminId, "AdminGrant", ct);
		await Db.SaveChangesAsync(ct);

		await Db.Entry(c).ReloadAsync(ct);
		var full = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role)
			.Include(x => x.Manager).Include(x => x.Badges)
			.FirstAsync(x => x.Id == collaboratorId, ct);
		return CollaboratorMapper.ToDto(full);
	}
```
Add required usings if missing: `using FluentValidation;`, `using Microsoft.EntityFrameworkCore;`, `using Waao.Domain.Models.Enums;`, `using Waao.Services.Mappers;`. If `AdminService`'s primary constructor lacked `GamificationEngine`/`IValidator<GrantXpDto>`, add them — DI resolves `GamificationEngine` (already `AddScoped`) and FluentValidation validators are auto-registered via `AddValidatorsFromAssemblyContaining` in `Program.cs` (no Program.cs change).

- [ ] **Step 7: Add the controller endpoint**

In `src/Waao.API/Controllers/AdminController.cs`, under `// ----- People -----`, add:
```csharp
	[HttpPost("collaborators/{id:guid}/grant-xp")]
	[ProducesResponseType(typeof(CollaboratorDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GrantXp(Guid id, [FromBody] GrantXpDto dto, CancellationToken ct)
		=> Ok(await Service.GrantXpAsync(id, dto, Me, ct));
```
(The class already has `[Authorize(Policy = "Admin")]` and the `Me` property — no extra auth wiring.)

- [ ] **Step 8: Run tests + build**

Run: `dotnet test tests/Waao.Tests --filter GrantXpTests`
Expected: PASS (3 tests).
Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add src/Waao.Services.Abstractions src/Waao.Services/Services/AdminService.cs src/Waao.Services/Validation/GrantXpValidator.cs src/Waao.API/Controllers/AdminController.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: admin manual XP grant endpoint"
```

---

### Task 7: Reset migration + single-higor seed

**Files:**
- Modify: `src/Waao.Infra.EF/Seeds/DbInitializer.cs` (`SeedDefaultAdminsAsync`)
- Create (via EF CLI): `src/Waao.Infra.EF/Migrations/<timestamp>_ManualXpEconomyReset.cs`
- Test: `tests/Waao.Tests/Seeds/SeedTests.cs`

- [ ] **Step 1: Write the failing test for the seed**

`tests/Waao.Tests/Seeds/SeedTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Infra.EF.Seeds;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Seeds;

public class SeedTests
{
	[Fact]
	public async Task Seed_CreatesOnlyHigorAdmin()
	{
		using var db = TestDb.New();
		await DbInitializer.SeedAsync(db);

		var users = await db.Collaborators.ToListAsync();
		users.Should().ContainSingle();
		users[0].Email.Should().Be("higor@waao.com.br");
		users[0].RoleKind.Should().Be(CollaboratorRoleKind.Admin);
		users[0].EmailVerified.Should().BeTrue();
		users[0].OnboardingCompletedAt.Should().NotBeNull();
		users[0].TotalXp.Should().Be(0);
		users[0].CurrentLevel.Should().Be(0);
	}
}
```

> `EmailVerified` is introduced by Feature C. **Sequencing note:** B ships before C. For B alone, the `EmailVerified` property may not yet exist on `Collaborator`. If it does not, drop the `EmailVerified` assertion in this test and the `EmailVerified`/`EmailVerifiedAt` lines in Step 3; Feature C's plan will add them and re-assert. Everything else in this task stands alone.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Waao.Tests --filter SeedTests`
Expected: FAIL — three users seeded (`admin@`, `hr@`, `demo@`), not a single higor.

- [ ] **Step 3: Replace the seeded users**

In `src/Waao.Infra.EF/Seeds/DbInitializer.cs`, replace the `users` array inside `SeedDefaultAdminsAsync` with a single entry:
```csharp
		(string Email, string Name, string Password, CollaboratorRoleKind Role, DateOnly JoinDate)[] users =
		[
			("higor@waao.com.br", "Higor", "Waao2026!", CollaboratorRoleKind.Admin, new DateOnly(2023, 1, 1)),
		];
```
In the same loop, when creating the `Collaborator`, always set onboarding as
complete for the bootstrap admin (the field exists from Task 2's entity edit):
```csharp
				OnboardingCompletedAt = DateTime.UtcNow,
```
And also set verified state **if Feature C fields exist** (skip these two lines for B-only):
```csharp
				EmailVerified = true,
				EmailVerifiedAt = DateTime.UtcNow,
```
(The loop already guards with `if (await db.Collaborators.AnyAsync(c => c.Email == email, ct)) continue;` so re-seeding is safe.)

- [ ] **Step 4: Run the seed test**

Run: `dotnet test tests/Waao.Tests --filter SeedTests`
Expected: PASS.

- [ ] **Step 5: Create the migration**

Run:
```bash
dotnet ef migrations add ManualXpEconomyReset -p src/Waao.Infra.EF -s src/Waao.API
```
Expected: a new migration class created under `src/Waao.Infra.EF/Migrations/`.

- [ ] **Step 6: Edit the migration `Up` to add the data reset**

Open the generated `<timestamp>_ManualXpEconomyReset.cs`. The model diff should
contain the **new `onboarding_completed_at` column** (nullable `timestamp with time zone`,
added because Task 2 added `Collaborator.OnboardingCompletedAt`) and may contain the
`current_level` default change. If the `AddColumn` for `onboarding_completed_at` is
present, keep it. If the `current_level` default change did NOT diff (defaults
sometimes don't), add the column default alter. Then append the destructive data
steps at the end of `Up` (after any generated `AddColumn`):
```csharp
			migrationBuilder.Sql("ALTER TABLE collaborators ALTER COLUMN current_level SET DEFAULT 0;");
			migrationBuilder.Sql("UPDATE collaborators SET total_xp = 0, current_level = 0;");
			migrationBuilder.Sql("DELETE FROM xp_transactions;");
			migrationBuilder.Sql("UPDATE collaborators SET is_deleted = true, deleted_at = now(), updated_at = now() WHERE lower(email) <> 'higor@waao.com.br' AND is_deleted = false;");
			// Folded in from onboarding spec: existing (pre-onboarding) users are
			// considered already onboarded so they are not nagged / gamification-gated.
			migrationBuilder.Sql("UPDATE collaborators SET onboarding_completed_at = now() WHERE onboarding_completed_at IS NULL AND is_deleted = false;");
```
> If the generated migration did NOT include the `onboarding_completed_at`
> `AddColumn` (e.g. the model snapshot was stale), add it explicitly in `Up`
> before the SQL block: `migrationBuilder.AddColumn<DateTime>(name: "onboarding_completed_at", table: "collaborators", type: "timestamp with time zone", nullable: true);` and the matching `DropColumn` in `Down`.
In `Down`, only revert the schema default (data steps are intentionally irreversible — add a comment):
```csharp
			// Data reset (XP wipe, ledger clear, user soft-delete) is intentionally NOT reversible.
			migrationBuilder.Sql("ALTER TABLE collaborators ALTER COLUMN current_level SET DEFAULT 1;");
```
> Verify actual column names with `\d collaborators` / `\d xp_transactions` against local Postgres (snake_case via EFCore.NamingConventions: expect `total_xp`, `current_level`, `is_deleted`, `deleted_at`, `updated_at`, table `xp_transactions`). Adjust SQL if a name differs.

- [ ] **Step 7: Apply + verify on local Postgres**

Run:
```bash
dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API
```
Then verify:
```bash
psql "Host=localhost;Port=5432;Database=WaaoLocal;Username=postgres;Password=postgres" \
  -c "SELECT count(*) FROM xp_transactions;" \
  -c "SELECT email,total_xp,current_level,is_deleted FROM collaborators;"
```
Expected: `xp_transactions` count 0; every collaborator `total_xp=0,current_level=0`; only `higor@waao.com.br` has `is_deleted=false`.

- [ ] **Step 8: Commit**

```bash
git add src/Waao.Infra.EF/Migrations src/Waao.Infra.EF/Seeds/DbInitializer.cs tests/Waao.Tests
git -c user.email="higor@waao.com.br" commit -m "feat: ManualXpEconomyReset migration + single higor admin seed"
```

---

### Task 8: Frontend — admin Grant XP UI, i18n, remove auto-celebrations

**Files:**
- Modify: `WaaoFrontend/src/services/admin.service.ts`
- Modify: `WaaoFrontend/src/pages/collaborators/admin-panel.tsx` (and/or `collaborator-detail-page.tsx`)
- Modify: `WaaoFrontend/src/locales/pt-BR/common.json`, `.../en/common.json`, `.../es/common.json`
- Modify: login + career-event flow components that render `xp-chip` / `level-up-overlay`

> This task runs in the **WaaoFrontend** repo (separate git repo). Commit/push there.

- [ ] **Step 1: Add the service call**

In `WaaoFrontend/src/services/admin.service.ts`, add to the exported service object (match existing axios pattern, `apiClient` base is `/api/waao`):
```ts
	async grantXp(collaboratorId: string, dto: { amount: number; reason: string }) {
		const { data } = await apiClient.post(`/admin/collaborators/${collaboratorId}/grant-xp`, dto);
		return data;
	},
```
(If `admin.service.ts` exports a typed `Collaborator` return elsewhere, type the return as that type rather than inferring; do not use `any`.)

- [ ] **Step 2: Add i18n keys (all 3 locales)**

Add the same key block to each of `WaaoFrontend/src/locales/{pt-BR,en,es}/common.json` under a new top-level `"admin"` object (create if absent). pt-BR is the source:

pt-BR:
```json
	"admin": {
		"grantXp": {
			"title": "Conceder XP",
			"amount": "Quantidade",
			"reason": "Motivo",
			"submit": "Conceder",
			"success": "XP concedido",
			"amountHint": "Use valores negativos para correções"
		}
	}
```
en:
```json
	"admin": {
		"grantXp": {
			"title": "Grant XP",
			"amount": "Amount",
			"reason": "Reason",
			"submit": "Grant",
			"success": "XP granted",
			"amountHint": "Use negative values for corrections"
		}
	}
```
es:
```json
	"admin": {
		"grantXp": {
			"title": "Conceder XP",
			"amount": "Cantidad",
			"reason": "Motivo",
			"submit": "Conceder",
			"success": "XP concedido",
			"amountHint": "Usa valores negativos para correcciones"
		}
	}
```
> If an `"admin"` key already exists in these files, merge `grantXp` into it instead of adding a second `"admin"` key (invalid JSON otherwise). Keep keys ordered consistently across the 3 files. Validate each file parses: `node -e "require('./WaaoFrontend/src/locales/en/common.json')"`.

- [ ] **Step 3: Add the Grant XP control**

In `WaaoFrontend/src/pages/collaborators/admin-panel.tsx` (open it first to match its component/UI conventions — it uses the app's own UI lib, `useTranslation`, react-query), add a small form per selected collaborator: number input (`amount`, allows negative), text input (`reason`), submit button calling `adminService.grantXp(id, { amount, reason })` via a `useMutation`; on success invalidate the collaborator/leaderboard queries and show the `admin.grantXp.success` toast. All visible strings via `t('admin.grantXp.*')`. No raw `any`. If admin-panel is list-only, place the control in `collaborator-detail-page.tsx` instead (whichever shows a single collaborator with admin actions).

- [ ] **Step 4: Remove auto XP/level-up celebrations**

Run in `WaaoFrontend/`: `grep -rn "xp-chip\|XpChip\|level-up-overlay\|LevelUpOverlay\|LoginStreakBonusXp\|loginStreakBonusXp\|levelAfter\|XpAwarded\|xpAwarded" src` to find render sites. In the **login** flow (`pages/auth/login-page.tsx` + wherever `AuthResult` is consumed) and the **career-event** creation flow, stop rendering the XP chip and level-up overlay (they are always 0 / no-op now). Keep `badge-unlock-toast` (badges still unlock). Do not delete the celebration components themselves (the admin grant flow may reuse the xp-chip to show the granted amount — optional, not required); just remove the automatic triggers on login/career events.

- [ ] **Step 5: Build the frontend**

Run: `cd WaaoFrontend && npm run build`
Expected: build succeeds (tsc + vite), no type errors, no `any`.

- [ ] **Step 6: Commit + push (WaaoFrontend repo)**

```bash
cd WaaoFrontend
git add src/services/admin.service.ts src/locales src/pages
git -c user.email="higor@waao.com.br" commit -m "feat: admin grant-xp UI, i18n keys, remove auto xp/level-up celebrations"
git push origin main   # Cloudflare Worker CI/CD auto-deploys
```

---

### Task 9: Full verification + backend deploy

- [ ] **Step 1: Full test run**

Run: `dotnet test tests/Waao.Tests`
Expected: all tests PASS.

- [ ] **Step 2: Release build**

Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Deploy backend**

Run: `cd /Users/higorflopes/RiderProjects/Repositories/Waao/WaaoBackend && fly deploy --remote-only`
Expected: build + 2 machines updated, deploy succeeds. (Startup runs the migration behind the existing Postgres advisory lock — watch logs for clean startup, no duplicate-key.)

- [ ] **Step 4: Smoke test**

```bash
curl -fsS https://waao-api.fly.dev/health
# log in as higor@waao.com.br / Waao2026!, hit POST /api/waao/admin/collaborators/{id}/grant-xp
```
Expected: `/health` healthy; non-higor users gone; XP/level 0 until a grant; admin grant returns updated collaborator.

- [ ] **Step 5: Update deploy journal**

Append a dated entry to the vault journal `Memory/Journals/WAAO/` noting the manual-XP migration shipped, the destructive reset, and the single-admin bootstrap (`higor@waao.com.br` / `Waao2026!` — rotate).

---

## Self-Review

**Spec coverage:** reset→Task 7; remove auto-XP (career/badge/streak)→Tasks 5/3/4; level-0→Task 2; admin grant→Task 6; single higor→Task 7; frontend grant UI + remove celebrations + i18n→Task 8; tests→every task + Task 9; deploy/rollout→Task 9. C-interaction handled via the `EmailVerified` sequencing notes in Task 7. All spec sections covered.

**Placeholder scan:** No "TBD/handle edge cases" — code shown for every code step. Two explicit conditional branches (Feature-C `EmailVerified` fields; admin-panel vs detail page) are decision instructions with concrete fallbacks, not placeholders.

**Type consistency:** `GrantXpDto {Amount:int, Reason:string}` consistent across DTO/validator/service/controller/test/frontend. `GrantXpAsync(Guid, GrantXpDto, Guid, CancellationToken):Task<CollaboratorDto>` identical in interface, impl, controller call, tests. `XpSource.Admin` (existing enum, value 99). `ComputeLevelAsync` returns `int` 0-based throughout. `Collaborator.OnboardingCompletedAt` is `DateTime?` everywhere (entity, gate checks in BadgeEvaluator/StreakTracker, seed, migration backfill, tests); admin grant path bypasses the gate intentionally (admin override) so GrantXpTests need no `OnboardingCompletedAt`.

**Onboarding-fold coverage:** gate added in Task 3 (BadgeEvaluator Step 3a + `NotOnboarded_NoBadgesUnlock` test) and Task 4 (StreakTracker Step 3a + `NotOnboarded_StreakDoesNotAdvance` test); column/backfill in Task 7 Step 6; bootstrap-admin onboarded seed + assertion in Task 7 Steps 3/1; entity field in Task 2 Step 4. The onboarding **wizard/endpoints/banner** are intentionally NOT here (separate Feature D).
