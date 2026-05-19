# Onboarding Wizard (UI + endpoints + banner) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the user-facing onboarding wizard (welcome screens + required profile step) at `/onboarding` plus the `GET status` / `POST complete` endpoints and a post-login "finish onboarding" banner; completing the wizard sets `Collaborator.OnboardingCompletedAt`, which unlocks the gamification gate (`BadgeEvaluator`/`StreakTracker`) shipped in Feature B.

**Architecture:** Thin `IOnboardingService` (read-side: per-field booleans + completed flag; write-side: validate + set `PhotoUrl`/`Bio`/`Birthdate`/`DepartmentId` + `OnboardingCompletedAt` + `UpdatedAt`; idempotent). New `OnboardingController` `[Authorize]` exposes `GET status` and `POST complete`. Frontend gains a public-only-to-authed `/onboarding` wizard, a `useOnboardingStatus` query, and a per-session-dismissible banner in the authenticated layout (browse-but-nagged — **no hard redirect**).

**Tech Stack:** .NET 9, EF Core 9 + Npgsql (snake_case), FluentValidation, xUnit + FluentAssertions + EF InMemory (existing `Waao.Tests`), React 19 + TS + axios + @tanstack/react-query + react-hook-form + zod + i18next.

**Spec:** `docs/superpowers/specs/2026-05-19-onboarding-design.md`

**Reconciliation with deployed reality (read before starting):**
- `Collaborator.OnboardingCompletedAt` (`DateTime?`) exists from Feature B. `BadgeEvaluator.EvaluateAsync` and `StreakTracker.RegisterLogin/ActivityAsync` already fail-closed gate on it (`null` ⇒ no badges/streaks). Do NOT re-add the gate; D only flips `OnboardingCompletedAt` from null → now via the new endpoint.
- `Collaborator` already has `PhotoUrl`, `Bio`, `Birthdate`, `DepartmentId` (nullable). D writes those four.
- `higor@waao.com.br` is seeded onboarded. Migration `20260519200355_AddEmailVerification` is the latest applied; D adds NO migration (no schema change).
- Standards: TABS, file-scoped namespaces, primary-ctor DI (PascalCase params), DTOs as `record` with `init` setters, FluentValidation (no data annotations), `GuidGenerator`/`Guid.CreateVersion7()`, `DateTime.UtcNow`. Auth-state mutations stamp `c.UpdatedAt = DateTime.UtcNow;` (sibling pattern from `ChangePasswordAsync`/`VerifyEmailAsync`).
- Commits: conventional prefix, NO AI/Claude/Anthropic/Co-Authored-By, author `git -c user.email="higor@waao.com.br"`. `git add` ONLY task files (NEVER `-A`/`.`; `.claude-flow/` strays must stay out). Work on `main` (no branches). Implementer does NOT push; the controller pushes after both reviews pass.
- Frontend: app's OWN UI lib (`@/components/ui/*`), NOT `@medtrack/ui`, no raw HTML for new controls. `apiClient` base is `${API_BASE_URL}/api/waao`. NO `any`. pt-BR is the i18n source.
- Frontend URL is `https://waao-frontend.higorflopes.workers.dev` (Cloudflare Worker, NOT pages.dev). Backend has NO CI/CD — backend deploys via manual `fly deploy`. Frontend auto-deploys via Cloudflare on push.

---

### Task D1: Backend DTOs + `IOnboardingService` interface

**Files:**
- Create: `src/Waao.Services.Abstractions/Dtos/OnboardingDtos.cs`
- Modify: `src/Waao.Services.Abstractions/Services/IServices.cs` (add `IOnboardingService`)
- Test: `tests/Waao.Tests/Onboarding/OnboardingDtoTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Onboarding/OnboardingDtoTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run — expect FAIL (types not defined)**

Run: `dotnet test tests/Waao.Tests --filter OnboardingDtoTests` → FAIL (compile).

- [ ] **Step 3: Create the DTOs**

`src/Waao.Services.Abstractions/Dtos/OnboardingDtos.cs`:
```csharp
namespace Waao.Services.Abstractions.Dtos;

public record OnboardingStatusDto
{
	public bool Completed { get; init; }
	public DateTime? CompletedAt { get; init; }
	public bool PhotoSet { get; init; }
	public bool BioSet { get; init; }
	public bool BirthdateSet { get; init; }
	public bool DepartmentSet { get; init; }
}

public record CompleteOnboardingDto
{
	public string PhotoUrl { get; init; } = string.Empty;
	public string Bio { get; init; } = string.Empty;
	public DateOnly Birthdate { get; init; }
	public Guid DepartmentId { get; init; }
}
```

- [ ] **Step 4: Add the interface**

In `src/Waao.Services.Abstractions/Services/IServices.cs`, add (near the other service interfaces, matching the file's existing style):
```csharp
public interface IOnboardingService
{
	Task<OnboardingStatusDto> GetStatusAsync(Guid collaboratorId, CancellationToken ct = default);
	Task<OnboardingStatusDto> CompleteAsync(Guid collaboratorId, CompleteOnboardingDto dto, CancellationToken ct = default);
}
```

- [ ] **Step 5: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter OnboardingDtoTests` → PASS.
Run: `dotnet build src/Waao.Services.Abstractions/Waao.Services.Abstractions.csproj -c Release` → 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services.Abstractions/Dtos/OnboardingDtos.cs src/Waao.Services.Abstractions/Services/IServices.cs tests/Waao.Tests/Onboarding/OnboardingDtoTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: onboarding DTOs and IOnboardingService interface"
```

---

### Task D2: `CompleteOnboardingValidator`

**Files:**
- Create: `src/Waao.Services/Validation/CompleteOnboardingValidator.cs`
- Test: `tests/Waao.Tests/Onboarding/CompleteOnboardingValidatorTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Onboarding/CompleteOnboardingValidatorTests.cs`:
```csharp
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

	[Fact] public void EmptyPhoto_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { PhotoUrl = "" }).IsValid.Should().BeFalse();

	[Fact] public void EmptyBio_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { Bio = "" }).IsValid.Should().BeFalse();

	[Fact] public void BioTooLong_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { Bio = new string('x', 1001) }).IsValid.Should().BeFalse();

	[Fact] public void DefaultBirthdate_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { Birthdate = default }).IsValid.Should().BeFalse();

	[Fact] public void FutureBirthdate_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { Birthdate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }).IsValid.Should().BeFalse();

	[Fact] public void EmptyDepartment_Fails() =>
		new CompleteOnboardingValidator().Validate(Good() with { DepartmentId = Guid.Empty }).IsValid.Should().BeFalse();
}
```

- [ ] **Step 2: Run — expect FAIL (validator not defined)**

Run: `dotnet test tests/Waao.Tests --filter CompleteOnboardingValidatorTests` → FAIL (compile).

- [ ] **Step 3: Create the validator**

`src/Waao.Services/Validation/CompleteOnboardingValidator.cs`:
```csharp
using FluentValidation;
using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Validation;

public class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingDto>
{
	public CompleteOnboardingValidator()
	{
		RuleFor(x => x.PhotoUrl).NotEmpty().MaximumLength(500);
		RuleFor(x => x.Bio).NotEmpty().MaximumLength(1000);
		RuleFor(x => x.Birthdate)
			.NotEqual(default(DateOnly)).WithMessage("Birthdate is required.")
			.Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("Birthdate must be in the past.");
		RuleFor(x => x.DepartmentId).NotEqual(Guid.Empty);
	}
}
```
(Department existence is checked in the service against the DB — validators don't take a DbContext in this codebase.)

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter CompleteOnboardingValidatorTests` → PASS (7).

- [ ] **Step 5: Commit**

```bash
git add src/Waao.Services/Validation/CompleteOnboardingValidator.cs tests/Waao.Tests/Onboarding/CompleteOnboardingValidatorTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: CompleteOnboardingValidator"
```

---

### Task D3: `OnboardingService` impl + DI + service tests

**Files:**
- Create: `src/Waao.Services/Services/OnboardingService.cs`
- Modify: `src/Waao.API/Program.cs` (`AddScoped<IOnboardingService, OnboardingService>()` — match existing AddScoped line style/placement near the other service registrations)
- Create: `tests/Waao.Tests/Support/OnboardingServiceFactory.cs`
- Test: `tests/Waao.Tests/Onboarding/OnboardingServiceTests.cs`

- [ ] **Step 1: Support factory**

`tests/Waao.Tests/Support/OnboardingServiceFactory.cs`:
```csharp
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
```
(If `OnboardingService`'s final primary-ctor signature differs from `(WaaoDbContext, IValidator<CompleteOnboardingDto>, ILogger<OnboardingService>)`, iterate the factory to match the real ctor.)

- [ ] **Step 2: Failing test**

`tests/Waao.Tests/Onboarding/OnboardingServiceTests.cs`:
```csharp
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Onboarding;

public sealed class OnboardingServiceTests
{
	private static (Guid CollaboratorId, Guid DepartmentId) Seed(Microsoft.EntityFrameworkCore.DbContext db)
	{
		var dept = new Department { Id = Guid.CreateVersion7(), Name = "Eng", Description = "", ColorHex = "#000" };
		var c = new Collaborator { Id = Guid.CreateVersion7(), FullName = "T", Email = "t@waao.com.br", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) };
		db.Add(dept);
		db.Add(c);
		db.SaveChanges();
		return (c.Id, dept.Id);
	}

	[Fact]
	public async Task GetStatus_NotOnboarded_AllFlagsFalse()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, _) = Seed(db);
		var s = await svc.GetStatusAsync(cid);
		s.Completed.Should().BeFalse();
		s.CompletedAt.Should().BeNull();
		s.PhotoSet.Should().BeFalse();
		s.BioSet.Should().BeFalse();
		s.BirthdateSet.Should().BeFalse();
		s.DepartmentSet.Should().BeFalse();
	}

	[Fact]
	public async Task Complete_SetsFields_AndOnboardingCompletedAt()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		var s = await svc.CompleteAsync(cid, new CompleteOnboardingDto
		{
			PhotoUrl = "https://x/p.png",
			Bio = "Hello",
			Birthdate = new DateOnly(1990, 1, 1),
			DepartmentId = did,
		});
		s.Completed.Should().BeTrue();
		s.CompletedAt.Should().NotBeNull();
		var c = await db.Collaborators.FirstAsync();
		c.OnboardingCompletedAt.Should().NotBeNull();
		c.PhotoUrl.Should().Be("https://x/p.png");
		c.Bio.Should().Be("Hello");
		c.Birthdate.Should().Be(new DateOnly(1990, 1, 1));
		c.DepartmentId.Should().Be(did);
		c.UpdatedAt.Should().NotBe(default);
	}

	[Fact]
	public async Task Complete_AlreadyCompleted_Idempotent_NoExtraWrite()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		var first = await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990,1,1), DepartmentId = did });
		var firstAt = first.CompletedAt;
		var snd = await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u2", Bio = "b2", Birthdate = new DateOnly(1991,1,1), DepartmentId = did });
		snd.Completed.Should().BeTrue();
		snd.CompletedAt.Should().Be(firstAt); // timestamp not bumped
		var c = await db.Collaborators.FirstAsync();
		c.PhotoUrl.Should().Be("u");          // still the first values, no overwrite
		c.Bio.Should().Be("b");
	}

	[Fact]
	public async Task Complete_UnknownDepartment_Throws()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, _) = Seed(db);
		var act = async () => await svc.CompleteAsync(cid, new CompleteOnboardingDto
		{
			PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990,1,1), DepartmentId = Guid.NewGuid(),
		});
		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task Complete_MissingCollaborator_Throws()
	{
		var (svc, _) = OnboardingServiceFactory.Create();
		var act = async () => await svc.CompleteAsync(Guid.NewGuid(), new CompleteOnboardingDto
		{
			PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990,1,1), DepartmentId = Guid.NewGuid(),
		});
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}

	[Fact]
	public async Task GetStatus_AfterComplete_ReturnsCompleted()
	{
		var (svc, db) = OnboardingServiceFactory.Create();
		var (cid, did) = Seed(db);
		await svc.CompleteAsync(cid, new CompleteOnboardingDto { PhotoUrl = "u", Bio = "b", Birthdate = new DateOnly(1990,1,1), DepartmentId = did });
		var s = await svc.GetStatusAsync(cid);
		s.Completed.Should().BeTrue();
		s.PhotoSet.Should().BeTrue();
		s.BioSet.Should().BeTrue();
		s.BirthdateSet.Should().BeTrue();
		s.DepartmentSet.Should().BeTrue();
	}
}
```

- [ ] **Step 3: Run — expect FAIL (`OnboardingService` not defined)**

Run: `dotnet test tests/Waao.Tests --filter OnboardingServiceTests` → FAIL (compile).

- [ ] **Step 4: Implement the service**

`src/Waao.Services/Services/OnboardingService.cs`:
```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Services;

public sealed class OnboardingService(
	WaaoDbContext Db,
	IValidator<CompleteOnboardingDto> CompleteValidator,
	ILogger<OnboardingService> Logger) : IOnboardingService
{
	public async Task<OnboardingStatusDto> GetStatusAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");
		return Map(c);
	}

	public async Task<OnboardingStatusDto> CompleteAsync(Guid collaboratorId, CompleteOnboardingDto dto, CancellationToken ct = default)
	{
		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		// Idempotent: already-onboarded users get the current status, no write/overwrite.
		if (c.OnboardingCompletedAt is not null)
			return Map(c);

		await CompleteValidator.ValidateAndThrowAsync(dto, ct);
		if (!await Db.Departments.AnyAsync(d => d.Id == dto.DepartmentId, ct))
			throw new ValidationException(
				[new FluentValidation.Results.ValidationFailure(nameof(dto.DepartmentId), "Department not found.")]);

		c.PhotoUrl = dto.PhotoUrl;
		c.Bio = dto.Bio;
		c.Birthdate = dto.Birthdate;
		c.DepartmentId = dto.DepartmentId;
		c.OnboardingCompletedAt = DateTime.UtcNow;
		c.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("Collaborator {Id} completed onboarding.", c.Id);
		return Map(c);
	}

	private static OnboardingStatusDto Map(Domain.Models.Entities.Collaborator c) => new()
	{
		Completed = c.OnboardingCompletedAt is not null,
		CompletedAt = c.OnboardingCompletedAt,
		PhotoSet = !string.IsNullOrWhiteSpace(c.PhotoUrl),
		BioSet = !string.IsNullOrWhiteSpace(c.Bio),
		BirthdateSet = c.Birthdate is not null && c.Birthdate != default(DateOnly),
		DepartmentSet = c.DepartmentId is not null && c.DepartmentId != Guid.Empty,
	};
}
```

- [ ] **Step 5: DI registration**

In `src/Waao.API/Program.cs`, find the `// Services` block (right after `AddScoped<IAuthService, AuthService>()`) and add a sibling line:
```csharp
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
```
(Add `using Waao.Services.Services;` if not present; it usually is via the existing service registrations.)

- [ ] **Step 6: Run — expect PASS + clean build**

Run: `dotnet test tests/Waao.Tests --filter OnboardingServiceTests` → PASS (6).
Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release` → 0 errors, 0 warnings.
Run: `dotnet test tests/Waao.Tests` → ALL green (expect 33 prior + 2 (D1) + 7 (D2) + 6 (D3) = 48).

- [ ] **Step 7: Commit**

```bash
git add src/Waao.Services/Services/OnboardingService.cs src/Waao.API/Program.cs tests/Waao.Tests/Support/OnboardingServiceFactory.cs tests/Waao.Tests/Onboarding/OnboardingServiceTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: OnboardingService (GetStatus, Complete idempotent) + DI"
```

---

### Task D4: `OnboardingController` + endpoint smoke test

**Files:**
- Create: `src/Waao.API/Controllers/OnboardingController.cs`
- Test: `tests/Waao.Tests/Onboarding/OnboardingControllerTests.cs` (lightweight — confirm the wiring + attributes via reflection; full behavior already covered by `OnboardingServiceTests`)

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Onboarding/OnboardingControllerTests.cs`:
```csharp
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
```

- [ ] **Step 2: Run — expect FAIL (controller not defined)**

Run: `dotnet test tests/Waao.Tests --filter OnboardingControllerTests` → FAIL (compile).

- [ ] **Step 3: Implement the controller**

`src/Waao.API/Controllers/OnboardingController.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/onboarding")]
[Authorize]
public class OnboardingController(IOnboardingService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("status")]
	[ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GetStatus(CancellationToken ct)
		=> Ok(await Service.GetStatusAsync(Me, ct));

	[HttpPost("complete")]
	[ProducesResponseType(typeof(OnboardingStatusDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Complete([FromBody] CompleteOnboardingDto dto, CancellationToken ct)
		=> Ok(await Service.CompleteAsync(Me, dto, ct));
}
```

- [ ] **Step 4: Run + build + full suite**

Run: `dotnet test tests/Waao.Tests --filter OnboardingControllerTests` → PASS (3).
Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release` → 0 errors, 0 warnings.
Run: `dotnet test tests/Waao.Tests` → ALL green (expect 51).

- [ ] **Step 5: Commit**

```bash
git add src/Waao.API/Controllers/OnboardingController.cs tests/Waao.Tests/Onboarding/OnboardingControllerTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: OnboardingController (GET status + POST complete)"
```

---

### Task D5: Frontend `onboarding.service.ts`

**Files (`/Users/higorflopes/RiderProjects/Repositories/Waao/WaaoFrontend`):**
- Create: `src/services/onboarding.service.ts`
- Modify: `src/types/waao.types.ts` (add `OnboardingStatus` and `CompleteOnboarding` types — confirm the file's existing type-name/style first)

- [ ] **Step 1: Read** the existing `src/services/auth.service.ts` and `src/lib/api-client.ts` for the conventional style; confirm the Collaborator type's field names (`photoUrl`/`bio`/`birthdate`/`departmentId`) in `src/types/waao.types.ts`.

- [ ] **Step 2: Add the types**

In `src/types/waao.types.ts` (match the file's existing TypeScript style — interface vs type — and casing; the JSON wire format is camelCase per the backend's JSON options):
```ts
export interface OnboardingStatus {
	completed: boolean;
	completedAt: string | null;
	photoSet: boolean;
	bioSet: boolean;
	birthdateSet: boolean;
	departmentSet: boolean;
}

export interface CompleteOnboardingDto {
	photoUrl: string;
	bio: string;
	birthdate: string;   // ISO date 'YYYY-MM-DD'
	departmentId: string;
}
```

- [ ] **Step 3: Create the service**

`src/services/onboarding.service.ts`:
```ts
import { apiClient } from '@/lib/api-client';
import type { CompleteOnboardingDto, OnboardingStatus } from '@/types/waao.types';

export const onboardingService = {
	async getStatus(): Promise<OnboardingStatus> {
		const { data } = await apiClient.get<OnboardingStatus>('/onboarding/status');
		return data;
	},
	async complete(dto: CompleteOnboardingDto): Promise<OnboardingStatus> {
		const { data } = await apiClient.post<OnboardingStatus>('/onboarding/complete', dto);
		return data;
	},
};
```
(If the project uses a class-with-singleton pattern instead of an object, mirror that style — match `auth.service.ts`/`admin.service.ts`.)

- [ ] **Step 4: Build**

Run: `npm run build` → 0 TS errors, no `any`, no unused-locals.

- [ ] **Step 5: Commit**

```bash
git add src/services/onboarding.service.ts src/types/waao.types.ts
git -c user.email="higor@waao.com.br" commit -m "feat: onboarding frontend service + types"
```

---

### Task D6: `useOnboardingStatus` hook + i18n keys

**Files:**
- Create: `src/hooks/use-onboarding-status.ts`
- Modify: `src/locales/pt-BR/common.json`, `src/locales/en/common.json`, `src/locales/es/common.json`

- [ ] **Step 1: Hook**

`src/hooks/use-onboarding-status.ts`:
```ts
import { useQuery } from '@tanstack/react-query';
import { onboardingService } from '@/services/onboarding.service';

export const ONBOARDING_STATUS_KEY = ['onboarding', 'status'] as const;

export function useOnboardingStatus(options?: { enabled?: boolean }) {
	return useQuery({
		queryKey: ONBOARDING_STATUS_KEY,
		queryFn: () => onboardingService.getStatus(),
		enabled: options?.enabled ?? true,
		staleTime: 60_000,
	});
}
```

- [ ] **Step 2: i18n (all 3 locales)**

Add to the existing `common.json` files under a new top-level `"onboarding"` object (MERGE if it exists; do not duplicate). pt-BR is the source. Keys: `onboarding.banner.message`, `onboarding.banner.cta`, `onboarding.banner.dismiss`, `onboarding.wizard.title`, `onboarding.wizard.welcome.intro1Title`, `onboarding.wizard.welcome.intro1Body`, `onboarding.wizard.welcome.intro2Title`, `onboarding.wizard.welcome.intro2Body`, `onboarding.wizard.welcome.next`, `onboarding.wizard.welcome.back`, `onboarding.wizard.profile.title`, `onboarding.wizard.profile.photoLabel`, `onboarding.wizard.profile.photoPlaceholder`, `onboarding.wizard.profile.bioLabel`, `onboarding.wizard.profile.bioPlaceholder`, `onboarding.wizard.profile.birthdateLabel`, `onboarding.wizard.profile.departmentLabel`, `onboarding.wizard.profile.submit`, `onboarding.wizard.errors.photoRequired`, `onboarding.wizard.errors.bioRequired`, `onboarding.wizard.errors.bioTooLong`, `onboarding.wizard.errors.birthdateRequired`, `onboarding.wizard.errors.birthdatePast`, `onboarding.wizard.errors.departmentRequired`, `onboarding.wizard.errors.generic`, `onboarding.wizard.success`. Real translations across pt-BR / en / es. Validate JSON: `node -e "JSON.parse(require('fs').readFileSync('src/locales/<l>/common.json','utf8'))"` for all 3.

- [ ] **Step 3: Build**

Run: `npm run build` → 0 TS errors.

- [ ] **Step 4: Commit**

```bash
git add src/hooks/use-onboarding-status.ts src/locales/pt-BR/common.json src/locales/en/common.json src/locales/es/common.json
git -c user.email="higor@waao.com.br" commit -m "feat: useOnboardingStatus hook + onboarding i18n (3 locales)"
```

---

### Task D7: `OnboardingPage` (welcome + profile wizard) + public-but-authed route

**Files:**
- Create: `src/pages/onboarding/onboarding-page.tsx`
- Modify: `src/App.tsx` (add the `/onboarding` route inside the authenticated layout — same place `/dashboard`/`/collaborators` live; auth is required, but the route does NOT trigger a redirect for `!completed` — the banner does that)

- [ ] **Step 1: Read** how authenticated routes are declared in `src/App.tsx` (after Feature C, `/verify-email` was added outside `RequireAuth`; `/onboarding` is INSIDE `RequireAuth` because the user must already be logged in to onboard). Read an existing form page (e.g. `register-page.tsx`) for RHF+zod conventions and an existing department-select or pattern in the app — if there is no `DepartmentSelect` component, the wizard uses a plain `Input` for `departmentId` is unacceptable; instead fetch the departments list via `apiClient.get('/catalog/departments')` (the existing `CatalogController` endpoint) and render a basic `<Select>` from `@/components/ui/*` (read which select component exists; if none, fall back to a styled `<select>` element with the app's existing Tailwind classes — search for an existing `<select>` in the codebase to match its styling).

- [ ] **Step 2: Wizard page**

`src/pages/onboarding/onboarding-page.tsx`:
```tsx
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { apiClient } from '@/lib/api-client';
import { onboardingService } from '@/services/onboarding.service';
import { ONBOARDING_STATUS_KEY, useOnboardingStatus } from '@/hooks/use-onboarding-status';
import type { Department } from '@/types/waao.types';

type WizardStep = 'intro1' | 'intro2' | 'profile';

function makeSchema(t: (k: string) => string) {
	return z.object({
		photoUrl: z.string().min(1, 'onboarding.wizard.errors.photoRequired').max(500),
		bio: z.string().min(1, 'onboarding.wizard.errors.bioRequired').max(1000, 'onboarding.wizard.errors.bioTooLong'),
		birthdate: z.string().min(1, 'onboarding.wizard.errors.birthdateRequired')
			.refine(v => new Date(v) < new Date(), 'onboarding.wizard.errors.birthdatePast'),
		departmentId: z.string().uuid('onboarding.wizard.errors.departmentRequired'),
	});
}
type Values = z.infer<ReturnType<typeof makeSchema>>;

export default function OnboardingPage() {
	const { t } = useTranslation();
	const navigate = useNavigate();
	const qc = useQueryClient();
	const status = useOnboardingStatus();
	const [step, setStep] = useState<WizardStep>('intro1');

	const schema = useMemo(() => makeSchema(t), [t]);
	const form = useForm<Values>({
		resolver: zodResolver(schema),
		defaultValues: { photoUrl: '', bio: '', birthdate: '', departmentId: '' },
	});

	const departments = useQuery({
		queryKey: ['catalog', 'departments'],
		queryFn: async () => (await apiClient.get<Department[]>('/catalog/departments')).data,
		staleTime: 60_000,
	});

	const complete = useMutation({
		mutationFn: (dto: Values) => onboardingService.complete(dto),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ONBOARDING_STATUS_KEY });
			navigate('/');
		},
	});

	if (status.data?.completed) {
		// Already done — go home (idempotent landing on /onboarding shouldn't trap the user).
		navigate('/', { replace: true });
		return null;
	}

	return (
		<div className="max-w-xl mx-auto py-12">
			<Card>
				<CardHeader><CardTitle>{t('onboarding.wizard.title')}</CardTitle></CardHeader>
				<CardContent className="space-y-4">
					{step === 'intro1' && (
						<>
							<h3 className="font-semibold">{t('onboarding.wizard.welcome.intro1Title')}</h3>
							<p>{t('onboarding.wizard.welcome.intro1Body')}</p>
							<Button onClick={() => setStep('intro2')}>{t('onboarding.wizard.welcome.next')}</Button>
						</>
					)}
					{step === 'intro2' && (
						<>
							<h3 className="font-semibold">{t('onboarding.wizard.welcome.intro2Title')}</h3>
							<p>{t('onboarding.wizard.welcome.intro2Body')}</p>
							<div className="flex gap-2">
								<Button variant="secondary" onClick={() => setStep('intro1')}>{t('onboarding.wizard.welcome.back')}</Button>
								<Button onClick={() => setStep('profile')}>{t('onboarding.wizard.welcome.next')}</Button>
							</div>
						</>
					)}
					{step === 'profile' && (
						<form onSubmit={form.handleSubmit(v => complete.mutate(v))} className="space-y-3">
							<div>
								<Label>{t('onboarding.wizard.profile.photoLabel')}</Label>
								<Input placeholder={t('onboarding.wizard.profile.photoPlaceholder')} {...form.register('photoUrl')} />
								{form.formState.errors.photoUrl && <p className="text-xs text-destructive">{t(form.formState.errors.photoUrl.message ?? '')}</p>}
							</div>
							<div>
								<Label>{t('onboarding.wizard.profile.bioLabel')}</Label>
								<Input placeholder={t('onboarding.wizard.profile.bioPlaceholder')} {...form.register('bio')} />
								{form.formState.errors.bio && <p className="text-xs text-destructive">{t(form.formState.errors.bio.message ?? '')}</p>}
							</div>
							<div>
								<Label>{t('onboarding.wizard.profile.birthdateLabel')}</Label>
								<Input type="date" {...form.register('birthdate')} />
								{form.formState.errors.birthdate && <p className="text-xs text-destructive">{t(form.formState.errors.birthdate.message ?? '')}</p>}
							</div>
							<div>
								<Label>{t('onboarding.wizard.profile.departmentLabel')}</Label>
								<select className="w-full h-10 rounded-md border bg-background px-2" {...form.register('departmentId')}>
									<option value="">—</option>
									{(departments.data ?? []).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
								</select>
								{form.formState.errors.departmentId && <p className="text-xs text-destructive">{t(form.formState.errors.departmentId.message ?? '')}</p>}
							</div>
							<div className="flex gap-2">
								<Button type="button" variant="secondary" onClick={() => setStep('intro2')}>{t('onboarding.wizard.welcome.back')}</Button>
								<Button type="submit" disabled={complete.isPending}>{t('onboarding.wizard.profile.submit')}</Button>
							</div>
							{complete.isError && <p className="text-sm text-destructive">{t('onboarding.wizard.errors.generic')}</p>}
						</form>
					)}
				</CardContent>
			</Card>
		</div>
	);
}
```
(Adjust component imports to the real paths/names this app uses. If the app already has a typed `Department` model + a Select component, use them. If `Department` isn't exported from `waao.types`, import from wherever it lives or define inline locally — read first.)

- [ ] **Step 3: Wire the route**

In `src/App.tsx`, add a `Route path="/onboarding" element={<OnboardingPage />}` INSIDE the `RequireAuth`-wrapped routes (alongside `/dashboard`/`/collaborators`/etc.). Lazy or eager import to match the file's existing style.

- [ ] **Step 4: Build**

Run: `npm run build` → 0 TS errors, no `any`, no unused-locals.

- [ ] **Step 5: Commit**

```bash
git add src/pages/onboarding/onboarding-page.tsx src/App.tsx
git -c user.email="higor@waao.com.br" commit -m "feat: onboarding wizard page (welcome + profile)"
```

---

### Task D8: "Finish onboarding" banner in the authenticated layout

**Files:**
- Create: `src/components/onboarding/onboarding-banner.tsx`
- Modify: whichever component renders the authenticated app shell/layout (read `src/App.tsx` and the existing sidebar/topbar to find the right place — most apps have an `AppShell`/`AuthenticatedLayout` or render the children inside `RequireAuth`). Add the banner ABOVE the page content so it appears on every authenticated page.

- [ ] **Step 1: Read** the auth-context/store (`use-auth.ts`) to get the current user id (for per-user localStorage dismissal key) and the layout file structure.

- [ ] **Step 2: Banner**

`src/components/onboarding/onboarding-banner.tsx`:
```tsx
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { useOnboardingStatus } from '@/hooks/use-onboarding-status';
import { useAuth } from '@/hooks/use-auth';

export function OnboardingBanner() {
	const { t } = useTranslation();
	const me = useAuth(s => s.me);
	const { data } = useOnboardingStatus({ enabled: Boolean(me) });
	const key = useMemo(() => me ? `waao.onboardingBannerDismissed.${me.id}` : null, [me]);
	const [dismissed, setDismissed] = useState<boolean>(() => key ? localStorage.getItem(key) === '1' : false);

	if (!me || !data || data.completed || dismissed) return null;

	return (
		<div className="bg-amber-100 border-b border-amber-300 text-amber-950 px-4 py-2 flex items-center gap-3 text-sm">
			<span className="flex-1">{t('onboarding.banner.message')}</span>
			<Link to="/onboarding"><Button size="sm">{t('onboarding.banner.cta')}</Button></Link>
			<Button
				size="sm"
				variant="ghost"
				onClick={() => { if (key) localStorage.setItem(key, '1'); setDismissed(true); }}
				aria-label={t('onboarding.banner.dismiss')}
			>
				✕
			</Button>
		</div>
	);
}
```
(Match the existing app's Tailwind palette/sizes — if there is already a notice/banner pattern in `src/components/`, reuse its visual style. If `useAuth(s => s.me)` typing differs, adapt to the real selector signature; if the user object's id property is not `id`, use the real name.)

- [ ] **Step 3: Mount the banner in the authenticated shell**

In the same file where the authenticated layout renders pages (often `App.tsx` inside `RequireAuth`'s wrapper, or a sidebar/topbar parent), render `<OnboardingBanner />` ABOVE the routed page content so it appears on every authenticated route INCLUDING the dashboard but NOT on `/onboarding` itself (the simplest: render unconditionally — the banner's own logic hides it when `data.completed` is true; on `/onboarding` the wizard navigates home on completion so the banner naturally disappears next render). If the layout file is hard to locate, place it directly inside `RequireAuth` in `src/App.tsx`.

- [ ] **Step 4: Build**

Run: `npm run build` → 0 TS errors.

- [ ] **Step 5: Commit**

```bash
git add src/components/onboarding/onboarding-banner.tsx src/App.tsx
git -c user.email="higor@waao.com.br" commit -m "feat: persistent dismissible 'finish onboarding' banner"
```

---

### Task D9: Full verification + deploy

- [ ] **Step 1: Backend full suite + build**

Run: `dotnet test tests/Waao.Tests` → ALL green (expect 51).
Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release` → 0 errors, 0 warnings.

- [ ] **Step 2: Deploy backend (manual — WaaoBackend has no CI/CD)**

```bash
cd /Users/higorflopes/RiderProjects/Repositories/Waao/WaaoBackend
fly deploy --remote-only
```
No new migration in this feature; startup just rolls forward to the new image. Watch logs for clean startup (`Application started`, no exceptions). `/health` should be healthy.

- [ ] **Step 3: Smoke (backend)**

Authenticate as `higor` (you have the real credential), then:
```bash
TOKEN="$(curl -s -X POST https://waao-api.fly.dev/api/waao/auth/login -H 'Content-Type: application/json' -d '{"email":"higor@waao.com.br","password":"<REAL>"}' | jq -r .token)"
curl -i -H "Authorization: Bearer $TOKEN" https://waao-api.fly.dev/api/waao/onboarding/status
# Expect 200 {completed:true,...} (higor is seeded onboarded)
```
Optionally register a fresh `@waao.com.br` user → verify (via fly logs link) → login → call `GET /onboarding/status` → expect `completed:false` with all per-field flags `false` → call `POST /onboarding/complete` with a valid body (use a real department id from `GET /api/waao/catalog/departments` — one is seeded by `DbInitializer`) → expect `200 {completed:true,...}`.

- [ ] **Step 4: Push frontend**

```bash
cd /Users/higorflopes/RiderProjects/Repositories/Waao/WaaoFrontend
git push origin main
```
Cloudflare auto-deploys the Worker. Verify the new `/onboarding` route returns 200 (SPA fallback serves index.html): `curl -sI "https://waao-frontend.higorflopes.workers.dev/onboarding"`.

- [ ] **Step 5: Update the deploy journal**

Append a dated section to `/Users/higorflopes/RiderProjects/Repositories/ClaudeMemory/Memory/Journals/WAAO/deploy-2026-05-19.md` summarizing: Feature D shipped (no migration; endpoints + wizard + banner; B's gamification gate now unlocks on completion); release version of `waao-api`; CF Worker commit; smoke results. Note that the file is not under git — it's published to WaaoDocs via the separate workflow.

---

## Self-Review

**Spec coverage:** entity already in place (B)→Reconciliation; gate already in place (B)→Reconciliation; DTOs+interface→D1; validator (4 fields + rules)→D2; service (status, idempotent complete, dept-exists, missing-coll 404, UpdatedAt stamp)→D3; DI→D3 Step 5; controller (GET status, POST complete, [Authorize])→D4; frontend service→D5; hook+i18n (3 locales)→D6; wizard page + route INSIDE auth→D7; persistent dismissible banner in authenticated shell→D8; deploy + smoke (backend manual fly, frontend CF push) + journal→D9. Browse-but-nagged is satisfied because the banner does NOT redirect.

**Placeholder scan:** No TBD/"handle errors" — concrete code in every code step. Explicit "read the real file/type first" instructions are verification steps, not placeholders. Frontend pages provide real working code; "adjust to the actual app component names" is bounded guidance with concrete fallback (read first; if X exists use it; else use the working code shown).

**Type consistency:** `OnboardingStatusDto{Completed,CompletedAt,PhotoSet,BioSet,BirthdateSet,DepartmentSet}` and `CompleteOnboardingDto{PhotoUrl,Bio,Birthdate,DepartmentId}` are identical across DTO/validator/service/controller/factory/tests. Frontend `OnboardingStatus`/`CompleteOnboardingDto` use the camelCase wire mirror. `IOnboardingService.GetStatusAsync(Guid,ct):Task<OnboardingStatusDto>` and `CompleteAsync(Guid,CompleteOnboardingDto,ct):Task<OnboardingStatusDto>` identical interface/impl/controller/tests. `ONBOARDING_STATUS_KEY = ['onboarding','status']` reused by hook + invalidations. Banner storage key `waao.onboardingBannerDismissed.{userId}`.
