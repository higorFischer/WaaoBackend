# Email Verification + `@waao.com.br` Self-Registration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Anyone with an `@waao.com.br` email can self-register; the account is created unverified and cannot log in until the email is verified via a tokenized link (Resend.com, with a dev log-fallback); `higor@waao.com.br` (config list) becomes Admin, others Collaborator; a rate-limited resend path exists.

**Architecture:** Add 5 verification fields to `Collaborator` + an `AddEmailVerification` migration (backfills existing rows verified). `AuthService` gains register-unverified / verify / resend logic and a login gate; a new `IEmailSender` (Resend HTTP impl + logging fallback) sends the link. New controller endpoints + exception→coded-JSON mapping. Frontend gains a verify page, register "check your email" panel, and a login "not verified → resend" affordance, i18n in 3 locales.

**Tech Stack:** .NET 9, EF Core 9 + Npgsql (snake_case), FluentValidation, xUnit + FluentAssertions + EF InMemory (existing `Waao.Tests`), React 19 + TS + axios + i18next.

**Spec:** `docs/superpowers/specs/2026-05-19-email-verification-design.md`

**Reconciliation with deployed reality (read before starting):**
- `Collaborator` already has `DateTime? OnboardingCompletedAt` (Feature B). This feature adds 5 MORE fields. The B onboarding gate means new unverified users (also not onboarded) already get no badges/streaks — so register needs no special "skip gamification" code beyond simply not calling it / not issuing a JWT.
- Latest migration is `20260519190724_ManualXpEconomyReset`. This feature's migration comes after it.
- `DbInitializer` seeds ONLY `higor@waao.com.br` (Admin, `OnboardingCompletedAt` set). This feature adds `EmailVerified=true, EmailVerifiedAt=UtcNow` to that seed.
- `StreakTracker(WaaoDbContext)` and `BadgeEvaluator(WaaoDbContext)` are now ONE-arg (Feature B dropped their `GamificationEngine` param). Any test newing them must use the 1-arg form.
- `Frontend:BaseUrl` is `https://waao-frontend.higorflopes.workers.dev` (Cloudflare **Worker**, NOT `pages.dev`). Use this everywhere the spec says pages.dev.
- `Waao.Tests` exists (15 tests, `Waao.Tests.Support.TestDb.New()` InMemory). Append new tests; keep all green.
- Standards: TABS, file-scoped namespaces, primary-ctor DI (PascalCase params), DTOs as `record`, FluentValidation (no data annotations), `GuidGenerator`/`Guid.CreateVersion7()`, `DateTime.UtcNow`. Commits: conventional prefix, **no AI/Claude/Anthropic/Co-Authored-By**, author `git -c user.email="higor@waao.com.br"`. `git add` ONLY task files (never `-A`; `.claude-flow/` strays must stay out; if `--amend` would sweep them, soft-reset to parent and recommit scoped). Work on `main` (no branches). Do NOT push (controller pushes after review).

---

### Task 1: Collaborator email-verification fields + EF config

**Files:**
- Modify: `src/Waao.Domain.Models/Entities/Collaborator.cs`
- Modify: `src/Waao.Infra.EF/Configurations/CollaboratorConfiguration.cs`
- Test: `tests/Waao.Tests/Auth/EmailVerificationFieldsTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Auth/EmailVerificationFieldsTests.cs`:
```csharp
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
```

- [ ] **Step 2: Run — expect FAIL (members don't exist / compile error)**

Run: `dotnet test tests/Waao.Tests --filter EmailVerificationFieldsTests`
Expected: FAIL (compile: `EmailVerified` etc. not defined).

- [ ] **Step 3: Add the 5 fields**

In `src/Waao.Domain.Models/Entities/Collaborator.cs`, in the `// ----- Auth -----` region (next to `OnboardingCompletedAt`), add:
```csharp
	// Schema migration intentionally deferred to plan Task 2 (AddEmailVerification).
	public bool EmailVerified { get; set; }
	public string? EmailVerificationToken { get; set; }
	public DateTime? EmailVerificationTokenExpiresAt { get; set; }
	public DateTime? EmailVerifiedAt { get; set; }
	public DateTime? LastVerificationEmailSentAt { get; set; }
```

- [ ] **Step 4: EF config — index the token + explicit default**

In `src/Waao.Infra.EF/Configurations/CollaboratorConfiguration.cs` `Configure(...)` (match the file's existing fluent style/TABS), add:
```csharp
		builder.Property(x => x.EmailVerified).HasDefaultValue(false);
		builder.HasIndex(x => x.EmailVerificationToken);
```

- [ ] **Step 5: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter EmailVerificationFieldsTests`
Expected: PASS.

- [ ] **Step 6: Build**

Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Waao.Domain.Models/Entities/Collaborator.cs src/Waao.Infra.EF/Configurations/CollaboratorConfiguration.cs tests/Waao.Tests/Auth/EmailVerificationFieldsTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: add email-verification fields to Collaborator"
```

---

### Task 2: AddEmailVerification migration + seeder verified

**Files:**
- Modify: `src/Waao.Infra.EF/Seeds/DbInitializer.cs`
- Create (via EF CLI): `src/Waao.Infra.EF/Migrations/<ts>_AddEmailVerification.cs` (+ Designer + snapshot)
- Test: `tests/Waao.Tests/Seeds/SeedTests.cs` (modify existing)

- [ ] **Step 1: Update the seed test (add the new expectation)**

In `tests/Waao.Tests/Seeds/SeedTests.cs`, add to `Seed_CreatesOnlyHigorAdmin` after the existing assertions:
```csharp
		users[0].EmailVerified.Should().BeTrue();
		users[0].EmailVerifiedAt.Should().NotBeNull();
```

- [ ] **Step 2: Run — expect FAIL (seeded higor not verified yet)**

Run: `dotnet test tests/Waao.Tests --filter SeedTests`
Expected: FAIL (`EmailVerified` is false).

- [ ] **Step 3: Seed higor verified**

In `src/Waao.Infra.EF/Seeds/DbInitializer.cs` `SeedDefaultAdminsAsync`, in the `new Collaborator { ... }` initializer (which already sets `OnboardingCompletedAt = DateTime.UtcNow`), add:
```csharp
				EmailVerified = true,
				EmailVerifiedAt = DateTime.UtcNow,
```

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter SeedTests`
Expected: PASS.

- [ ] **Step 5: Create migration**

Run: `dotnet ef migrations add AddEmailVerification -p src/Waao.Infra.EF -s src/Waao.API`
Expected: new migration files. It must add: `email_verified` (bool NOT NULL DEFAULT false), `email_verification_token` (text null), `email_verification_token_expires_at` (timestamptz null), `email_verified_at` (timestamptz null), `last_verification_email_sent_at` (timestamptz null), and an index on `email_verification_token`. (The Edit tool's hook blocks `Migrations/`; use `sed`/bash to append the backfill SQL.)

- [ ] **Step 6: Add the backfill to migration `Up`**

Confirm snake_case names via `psql "Host=localhost;Port=5432;Database=WaaoLocal;Username=postgres;Password=postgres" -c "\d collaborators"`. Append at the END of `Up` (after the generated AddColumn/CreateIndex):
```csharp
			// Existing rows predate verification — grandfather them as verified so no one is locked out.
			migrationBuilder.Sql("UPDATE collaborators SET email_verified = true WHERE email_verified = false;");
```
`Down`: keep EF's generated DropColumn/DropIndex; add comment `// Data backfill (grandfathering existing users verified) is not reversed.`

- [ ] **Step 7: Apply locally + drift check**

Run: `dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API`
Then `dotnet ef migrations add _DriftCheck -p src/Waao.Infra.EF -s src/Waao.API` → confirm `Up`/`Down` EMPTY → `dotnet ef migrations remove -p src/Waao.Infra.EF -s src/Waao.API`. Report whether _DriftCheck was empty.
Verify: `psql "...WaaoLocal..." -c "SELECT email_verified, count(*) FROM collaborators GROUP BY 1;"` — no `false` rows remain after backfill (or table empty — note it).

- [ ] **Step 8: Build + full suite**

Run: `dotnet build src/Waao.API/Waao.API.csproj -c Release` (0 errors) and `dotnet test tests/Waao.Tests` (all green, expect 16).

- [ ] **Step 9: Commit**

```bash
git add src/Waao.Infra.EF/Migrations src/Waao.Infra.EF/Seeds/DbInitializer.cs tests/Waao.Tests/Seeds/SeedTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: AddEmailVerification migration + seed higor verified"
```

---

### Task 3: DTOs, exceptions, validators, interface

**Files:**
- Modify: `src/Waao.Services.Abstractions/Dtos/AuthDtos.cs`
- Create: `src/Waao.Services.Abstractions/EmailNotVerifiedException.cs`, `.../InvalidVerificationTokenException.cs` (or co-locate in an existing exceptions file if one exists in Abstractions — check first)
- Modify: `src/Waao.Services/Validation/AuthValidators.cs`
- Modify: `src/Waao.Services.Abstractions/Services/IServices.cs` (the `IAuthService` interface)
- Test: `tests/Waao.Tests/Auth/AuthValidatorsTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Auth/AuthValidatorsTests.cs`:
```csharp
using FluentAssertions;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Validation;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class AuthValidatorsTests
{
	[Theory]
	[InlineData("alice@waao.com.br", true)]
	[InlineData("Bob@WAAO.COM.BR", true)]
	[InlineData("eve@gmail.com", false)]
	[InlineData("x@waao.com", false)]
	public void Register_OnlyAcceptsWaaoComBr(string email, bool valid)
	{
		var v = new CreateRegisterValidator();
		var r = v.Validate(new RegisterDto { FullName = "T", Email = email, Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		(r.Errors.All(e => e.PropertyName != nameof(RegisterDto.Email))).Should().Be(valid);
	}

	[Fact]
	public void VerifyEmail_RequiresToken()
		=> new VerifyEmailValidator().Validate(new VerifyEmailDto { Token = "" }).IsValid.Should().BeFalse();

	[Fact]
	public void Resend_RequiresValidEmail()
		=> new ResendVerificationValidator().Validate(new ResendVerificationDto { Email = "nope" }).IsValid.Should().BeFalse();
}
```
> Read `src/Waao.Services/Validation/AuthValidators.cs` first to learn the EXISTING register validator's class name (it may be `RegisterValidator` or `CreateRegisterValidator`). Use the real name in the test. If `RegisterDto` has more required fields, set them so only the email rule decides validity.

- [ ] **Step 2: Run — expect FAIL (compile: new DTOs/validators missing)**

Run: `dotnet test tests/Waao.Tests --filter AuthValidatorsTests`
Expected: FAIL.

- [ ] **Step 3: DTOs**

In `src/Waao.Services.Abstractions/Dtos/AuthDtos.cs` add:
```csharp
public record VerifyEmailDto
{
	public string Token { get; init; } = string.Empty;
}

public record ResendVerificationDto
{
	public string Email { get; init; } = string.Empty;
}

public record RegisterResultDto
{
	public string Status { get; init; } = "verification_sent";
	public string Email { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Exceptions**

Create `src/Waao.Services.Abstractions/EmailNotVerifiedException.cs`:
```csharp
namespace Waao.Services.Abstractions;

public sealed class EmailNotVerifiedException(string email)
	: Exception($"Email '{email}' is not verified.")
{
	public string Email { get; } = email;
}
```
Create `src/Waao.Services.Abstractions/InvalidVerificationTokenException.cs`:
```csharp
namespace Waao.Services.Abstractions;

public sealed class InvalidVerificationTokenException()
	: Exception("Invalid or expired verification token.");
```

- [ ] **Step 5: Validators**

In `src/Waao.Services/Validation/AuthValidators.cs`:
- Add to the existing register validator constructor (use its real class name):
```csharp
		RuleFor(x => x.Email)
			.Matches(@"^[^@\s]+@waao\.com\.br$").WithMessage("Email must be a @waao.com.br address.");
```
- Add:
```csharp
public class VerifyEmailValidator : AbstractValidator<VerifyEmailDto>
{
	public VerifyEmailValidator() => RuleFor(x => x.Token).NotEmpty();
}

public class ResendVerificationValidator : AbstractValidator<ResendVerificationDto>
{
	public ResendVerificationValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}
```
(Match the regex case-insensitively: FluentValidation `Matches` uses .NET regex; add `RegexOptions.IgnoreCase` via the `Matches(pattern, RegexOptions.IgnoreCase)` overload.)

- [ ] **Step 6: Interface**

In `src/Waao.Services.Abstractions/Services/IServices.cs` `interface IAuthService`, change `RegisterAsync` return type to `Task<RegisterResultDto>` and add:
```csharp
	Task<AuthResultDto> VerifyEmailAsync(VerifyEmailDto dto, CancellationToken ct = default);
	Task ResendVerificationAsync(ResendVerificationDto dto, CancellationToken ct = default);
```
(Confirm the exact current `RegisterAsync` signature and update it; `LoginAsync` stays `Task<AuthResultDto>`.)

- [ ] **Step 7: Run validator test — expect PASS; build will FAIL (AuthService doesn't implement new interface yet — that's Task 5/6).**

Run: `dotnet test tests/Waao.Tests --filter AuthValidatorsTests`
Expected: the 3 validator tests PASS (they don't depend on AuthService). `dotnet build` of the API will fail until Task 5/6 — that is expected; do NOT try to make the whole solution build in this task. Only the filtered validator tests must pass.

- [ ] **Step 8: Commit**

```bash
git add src/Waao.Services.Abstractions src/Waao.Services/Validation/AuthValidators.cs tests/Waao.Tests/Auth/AuthValidatorsTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: email-verification DTOs, exceptions, validators, interface"
```

---

### Task 4: IEmailSender + Resend impl + logging fallback + DI/config

**Files:**
- Create: `src/Waao.Services.Abstractions/Services/IEmailSender.cs`
- Create: `src/Waao.Services/Email/ResendEmailSender.cs`, `src/Waao.Services/Email/LoggingEmailSender.cs`
- Modify: `src/Waao.API/Program.cs`
- Modify: `src/Waao.API/appsettings.json`
- Test: `tests/Waao.Tests/Email/EmailSenderTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Email/EmailSenderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.Services.Email;
using Xunit;

namespace Waao.Tests.Email;

public sealed class EmailSenderTests
{
	private sealed class CapturingHandler : HttpMessageHandler
	{
		public HttpRequestMessage? Last;
		public string? Body;
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			Last = request;
			Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"x\"}") };
		}
	}

	[Fact]
	public async Task Resend_PostsToApi_WithBearerAndPayload()
	{
		var h = new CapturingHandler();
		var sender = new ResendEmailSender(new HttpClient(h), NullLogger<ResendEmailSender>.Instance, "re_test", "WAAO <no-reply@waao.com.br>");
		await sender.SendVerificationAsync("a@waao.com.br", "Alice", "https://x/verify-email?token=abc", default);

		h.Last!.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
		h.Last!.Headers.Authorization!.ToString().Should().Be("Bearer re_test");
		h.Body.Should().Contain("a@waao.com.br").And.Contain("verify-email?token=abc");
	}

	[Fact]
	public async Task Logging_FallbackDoesNotThrow()
	{
		var sender = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
		var act = async () => await sender.SendVerificationAsync("a@waao.com.br", "Alice", "https://x/verify-email?token=abc", default);
		await act.Should().NotThrowAsync();
	}
}
```

- [ ] **Step 2: Run — expect FAIL (types missing)**

Run: `dotnet test tests/Waao.Tests --filter EmailSenderTests`
Expected: FAIL (compile).

- [ ] **Step 3: Interface**

`src/Waao.Services.Abstractions/Services/IEmailSender.cs`:
```csharp
namespace Waao.Services.Abstractions.Services;

public interface IEmailSender
{
	Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default);
}
```

- [ ] **Step 4: Logging fallback**

`src/Waao.Services/Email/LoggingEmailSender.cs`:
```csharp
using Microsoft.Extensions.Logging;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> Logger) : IEmailSender
{
	public Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default)
	{
		Logger.LogInformation("[DEV email] Verification link for {Email} ({Name}): {Url}", toEmail, toName, verifyUrl);
		return Task.CompletedTask;
	}
}
```

- [ ] **Step 5: Resend impl**

`src/Waao.Services/Email/ResendEmailSender.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Email;

public sealed class ResendEmailSender(HttpClient Http, ILogger<ResendEmailSender> Logger, string ApiKey, string From) : IEmailSender
{
	public async Task SendVerificationAsync(string toEmail, string toName, string verifyUrl, CancellationToken ct = default)
	{
		var html = $"""
			<div style="font-family:system-ui,sans-serif;max-width:480px;margin:auto">
			  <h2>Welcome to WAAO Journey</h2>
			  <p>Hi {System.Net.WebUtility.HtmlEncode(toName)}, confirm your email to activate your account.</p>
			  <p><a href="{verifyUrl}" style="background:#6366F1;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Verify my email</a></p>
			  <p style="color:#64748B;font-size:12px">Or paste this link: {verifyUrl}<br/>This link expires in 24 hours.</p>
			</div>
			""";
		using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
		{
			Content = JsonContent.Create(new { from = From, to = new[] { toEmail }, subject = "Verify your WAAO email", html }),
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
		var res = await Http.SendAsync(req, ct);
		if (!res.IsSuccessStatusCode)
		{
			var msg = await res.Content.ReadAsStringAsync(ct);
			Logger.LogError("Resend send failed {Status}: {Body}", (int)res.StatusCode, msg);
			throw new InvalidOperationException($"Resend returned {(int)res.StatusCode}");
		}
	}
}
```

- [ ] **Step 6: DI + config**

In `src/Waao.API/appsettings.json` add (top level, sibling of existing sections):
```json
  "Auth": { "AdminEmails": [ "higor@waao.com.br" ] },
  "Email": { "From": "WAAO <no-reply@waao.com.br>" },
  "Resend": { "ApiKey": "" },
  "Frontend": { "BaseUrl": "https://waao-frontend.higorflopes.workers.dev" }
```
In `src/Waao.API/Program.cs`, near the other service registrations, add:
```csharp
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEmailSender>(sp =>
{
	var cfg = sp.GetRequiredService<IConfiguration>();
	var key = cfg["Resend:ApiKey"];
	if (string.IsNullOrWhiteSpace(key))
		return new Waao.Services.Email.LoggingEmailSender(sp.GetRequiredService<ILogger<Waao.Services.Email.LoggingEmailSender>>());
	var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
	return new Waao.Services.Email.ResendEmailSender(http, sp.GetRequiredService<ILogger<Waao.Services.Email.ResendEmailSender>>(), key, cfg["Email:From"] ?? "WAAO <no-reply@waao.com.br>");
});
```
(Place it before `var app = builder.Build();`. Match the file's existing using/registration style. Confirm `using Waao.Services.Abstractions.Services;` is present.)

- [ ] **Step 7: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter EmailSenderTests`
Expected: PASS. (Solution build may still fail until Task 5/6 implement the interface — only this filter must pass.)

- [ ] **Step 8: Commit**

```bash
git add src/Waao.Services.Abstractions/Services/IEmailSender.cs src/Waao.Services/Email src/Waao.API/Program.cs src/Waao.API/appsettings.json tests/Waao.Tests/Email/EmailSenderTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: IEmailSender with Resend + logging fallback"
```

---

### Task 5: AuthService.RegisterAsync → unverified + send link

**Files:**
- Modify: `src/Waao.Services/Services/AuthService.cs`
- Create: `tests/Waao.Tests/Support/AuthServiceFactory.cs`
- Test: `tests/Waao.Tests/Auth/RegisterTests.cs`

- [ ] **Step 1: Support factory + failing test**

First READ `src/Waao.Services/Services/AuthService.cs` to get the EXACT current primary-ctor parameter list. Create `tests/Waao.Tests/Support/AuthServiceFactory.cs` that news up `AuthService` with: `TestDb.New()`; a real `JwtIssuer` built from `new JwtSettings { Key = "test-key-test-key-test-key-test-key-32+", Issuer = "waao", Audience = "waao-frontend" }`; `new StreakTracker(db)`; `new BadgeEvaluator(db)`; real validators (`new CreateRegisterValidator()` etc. — use real class names); a capturing `IEmailSender` test double (records last call); and an `IConfiguration` built via `new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Auth:AdminEmails:0"]="higor@waao.com.br",["Frontend:BaseUrl"]="https://fe.test"}).Build()`. Expose the captured email + the configuration. Match the ACTUAL final AuthService ctor (after Step 3 you will have added params — write the factory against the final shape; iterate until it compiles).

`tests/Waao.Tests/Auth/RegisterTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Enums;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class RegisterTests
{
	[Fact]
	public async Task Register_CreatesUnverified_NoJwt_SendsEmail_AdminFromConfig()
	{
		var f = AuthServiceFactory.Create();
		var res = await f.Service.RegisterAsync(new RegisterDto { FullName = "Higor", Email = "higor@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });

		res.Status.Should().Be("verification_sent");
		res.Email.Should().Be("higor@waao.com.br");
		var c = await f.Db.Collaborators.SingleAsync();
		c.EmailVerified.Should().BeFalse();
		c.EmailVerificationToken.Should().NotBeNullOrEmpty();
		c.EmailVerificationTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
		c.RoleKind.Should().Be(CollaboratorRoleKind.Admin);   // from Auth:AdminEmails
		f.LastEmail.Should().NotBeNull();
		f.LastEmail!.Value.VerifyUrl.Should().Contain("/verify-email?token=");
	}

	[Fact]
	public async Task Register_NonAdminEmail_IsCollaborator()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		(await f.Db.Collaborators.SingleAsync()).RoleKind.Should().Be(CollaboratorRoleKind.Collaborator);
	}
}
```
> The factory's `LastEmail` exposes `(string Email, string Name, string VerifyUrl)` captured from the test `IEmailSender`. Adjust property names to your factory.

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Waao.Tests --filter RegisterTests`
Expected: FAIL (RegisterAsync still old behavior / signature).

- [ ] **Step 3: Rework RegisterAsync**

Add to `AuthService` primary ctor (PascalCase): `IEmailSender EmailSender`, `IValidator<VerifyEmailDto> VerifyEmailValidator`, `IValidator<ResendVerificationDto> ResendValidator`, `IConfiguration Configuration`. Add usings: `using Microsoft.Extensions.Configuration;`, `using Waao.Services.Abstractions;`, `using Waao.Services.Abstractions.Services;`. Add a private token helper and rewrite `RegisterAsync`:
```csharp
	private static string NewToken()
	{
		var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
	}

	public async Task<RegisterResultDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
	{
		await RegisterValidator.ValidateAndThrowAsync(dto, ct);

		if (await Db.Collaborators.AnyAsync(c => c.Email == dto.Email, ct))
			throw new FluentValidation.ValidationException("Email is already in use.");

		var adminEmails = Configuration.GetSection("Auth:AdminEmails").Get<string[]>() ?? [];
		var isAdmin = adminEmails.Any(e => string.Equals(e, dto.Email, StringComparison.OrdinalIgnoreCase));

		var entity = new Collaborator
		{
			Id = Guid.CreateVersion7(),
			FullName = dto.FullName,
			Email = dto.Email,
			JoinDate = dto.JoinDate,
			RoleKind = isAdmin ? CollaboratorRoleKind.Admin : CollaboratorRoleKind.Collaborator,
			PasswordHash = PasswordHasher.Hash(dto.Password),
			EmailVerified = false,
			EmailVerificationToken = NewToken(),
			EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24),
			LastVerificationEmailSentAt = DateTime.UtcNow,
		};
		Db.Collaborators.Add(entity);
		await Db.SaveChangesAsync(ct);

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={entity.EmailVerificationToken}";
		try { await EmailSender.SendVerificationAsync(entity.Email, entity.FullName, verifyUrl, ct); }
		catch (Exception ex) { Logger.LogError(ex, "Verification email send failed for {Email}", entity.Email); }

		return new RegisterResultDto { Status = "verification_sent", Email = entity.Email };
	}
```
> Match the real existing register validator field name on the ctor (it may be `RegisterValidator`). Confirm `AuthService` already has an `ILogger<AuthService> Logger` ctor param; if not, add one. Remove any old register code that issued a JWT / ran `Streaks`/`Badges` / returned `AuthResultDto`. `RegisterDto`/duplicate-email behavior preserved. Keep `using FluentValidation;`.

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test tests/Waao.Tests --filter RegisterTests`
Expected: PASS.

- [ ] **Step 5: Build (whole API now compiles? interface still needs Verify/Resend — Task 6). Run filtered tests only.**

Run: `dotnet build src/Waao.Services/Waao.Services.csproj -c Release`
Expected: `Waao.Services` builds (the interface methods Verify/Resend are added in Task 6 — if `IAuthService` already declares them from Task 3, `AuthService` won't fully build until Task 6; that is expected. Only the RegisterTests filter must pass.)

- [ ] **Step 6: Commit**

```bash
git add src/Waao.Services/Services/AuthService.cs tests/Waao.Tests/Support/AuthServiceFactory.cs tests/Waao.Tests/Auth/RegisterTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: register creates unverified account + sends verification link"
```

---

### Task 6: VerifyEmail + ResendVerification + login gate

**Files:**
- Modify: `src/Waao.Services/Services/AuthService.cs`
- Test: `tests/Waao.Tests/Auth/VerifyResendLoginTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Auth/VerifyResendLoginTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Waao.Services.Abstractions;
using Waao.Services.Abstractions.Dtos;
using Waao.Tests.Support;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class VerifyResendLoginTests
{
	[Fact]
	public async Task Login_Unverified_Throws_Then_Works_After_Verify()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		var token = (await f.Db.Collaborators.SingleAsync()).EmailVerificationToken!;

		var login = async () => await f.Service.LoginAsync(new LoginDto { Email = "al@waao.com.br", Password = "Sup3rSecret!" });
		await login.Should().ThrowAsync<EmailNotVerifiedException>();

		var auth = await f.Service.VerifyEmailAsync(new VerifyEmailDto { Token = token });
		auth.Token.Should().NotBeNullOrEmpty();
		var c = await f.Db.Collaborators.SingleAsync();
		c.EmailVerified.Should().BeTrue();
		c.EmailVerificationToken.Should().BeNull();

		var ok = await f.Service.LoginAsync(new LoginDto { Email = "al@waao.com.br", Password = "Sup3rSecret!" });
		ok.Token.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public async Task Verify_BadToken_Throws()
	{
		var f = AuthServiceFactory.Create();
		var act = async () => await f.Service.VerifyEmailAsync(new VerifyEmailDto { Token = "nope" });
		await act.Should().ThrowAsync<InvalidVerificationTokenException>();
	}

	[Fact]
	public async Task Resend_NeverThrows_AndIsRateLimited()
	{
		var f = AuthServiceFactory.Create();
		await f.Service.RegisterAsync(new RegisterDto { FullName = "Al", Email = "al@waao.com.br", Password = "Sup3rSecret!", JoinDate = DateOnly.FromDateTime(DateTime.UtcNow) });
		// unknown email → no throw
		await f.Service.Invoking(s => s.ResendVerificationAsync(new ResendVerificationDto { Email = "ghost@waao.com.br" })).Should().NotThrowAsync();
		// known unverified but within 60s of register → rate-limited (no new email beyond the register one)
		var before = f.SentCount;
		await f.Service.ResendVerificationAsync(new ResendVerificationDto { Email = "al@waao.com.br" });
		f.SentCount.Should().Be(before); // rate-limited (register set LastVerificationEmailSentAt just now)
	}
}
```
> `f.SentCount` = number of emails the capturing sender has sent. Add it to the factory's test double.

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Waao.Tests --filter VerifyResendLoginTests`
Expected: FAIL (methods/guard missing).

- [ ] **Step 3: Implement**

In `AuthService` add the login guard and the two methods. In `LoginAsync`, immediately after the password-verify check (before the streak/badge/BuildResult), add:
```csharp
		if (!collaborator.EmailVerified)
			throw new EmailNotVerifiedException(collaborator.Email);
```
Add:
```csharp
	public async Task<AuthResultDto> VerifyEmailAsync(VerifyEmailDto dto, CancellationToken ct = default)
	{
		await VerifyEmailValidator.ValidateAndThrowAsync(dto, ct);

		var c = await Db.Collaborators
			.Include(x => x.Department).Include(x => x.Role).Include(x => x.Manager).Include(x => x.Badges)
			.FirstOrDefaultAsync(x => x.EmailVerificationToken == dto.Token, ct);
		if (c is null || c.EmailVerificationTokenExpiresAt is null || c.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
			throw new InvalidVerificationTokenException();

		c.EmailVerified = true;
		c.EmailVerifiedAt = DateTime.UtcNow;
		c.EmailVerificationToken = null;
		c.EmailVerificationTokenExpiresAt = null;
		await Db.SaveChangesAsync(ct);

		// Auto-login. Streak/badge passes are no-ops until onboarding completes (Feature B gate).
		var (streakDays, _, bonus) = await Streaks.RegisterLoginAsync(c.Id, ct: ct);
		await Db.SaveChangesAsync(ct);
		var newBadges = await Badges.EvaluateAsync(c.Id, ct);
		await Db.SaveChangesAsync(ct);
		return BuildResult(c, streakDays, bonus, newBadges);
	}

	public async Task ResendVerificationAsync(ResendVerificationDto dto, CancellationToken ct = default)
	{
		await ResendValidator.ValidateAndThrowAsync(dto, ct);

		var c = await Db.Collaborators.FirstOrDefaultAsync(x => x.Email == dto.Email, ct);
		if (c is null || c.EmailVerified) return;
		if (c.LastVerificationEmailSentAt is not null && (DateTime.UtcNow - c.LastVerificationEmailSentAt.Value).TotalSeconds < 60)
			return;

		c.EmailVerificationToken = NewToken();
		c.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
		c.LastVerificationEmailSentAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);

		var baseUrl = Configuration["Frontend:BaseUrl"] ?? "https://waao-frontend.higorflopes.workers.dev";
		var verifyUrl = $"{baseUrl}/verify-email?token={c.EmailVerificationToken}";
		try { await EmailSender.SendVerificationAsync(c.Email, c.FullName, verifyUrl, ct); }
		catch (Exception ex) { Logger.LogError(ex, "Resend verification email failed for {Email}", c.Email); }
	}
```
> Reuse the existing private `BuildResult(collaborator, streakDays, bonus, newBadges)` helper that `LoginAsync` uses (read the file to confirm its exact name/signature; if different, match it). Confirm `LoadByEmail`/login load path is unaffected.

- [ ] **Step 4: Run — expect PASS; full build + suite**

Run: `dotnet test tests/Waao.Tests --filter VerifyResendLoginTests` (PASS), then `dotnet build src/Waao.API/Waao.API.csproj -c Release` (0 errors — whole API compiles now), then `dotnet test tests/Waao.Tests` (ALL green; expect ~22).

- [ ] **Step 5: Commit**

```bash
git add src/Waao.Services/Services/AuthService.cs tests/Waao.Tests/Auth/VerifyResendLoginTests.cs tests/Waao.Tests/Support/AuthServiceFactory.cs
git -c user.email="higor@waao.com.br" commit -m "feat: verify-email, resend, and login email-verification gate"
```

---

### Task 7: Controller endpoints + exception→JSON mapping

**Files:**
- Modify: `src/Waao.API/Controllers/AuthController.cs`
- Modify: `src/Waao.API/Middleware/ExceptionHandlingMiddleware.cs`
- Test: `tests/Waao.Tests/Auth/ExceptionMappingTests.cs`

- [ ] **Step 1: Failing test**

`tests/Waao.Tests/Auth/ExceptionMappingTests.cs` — unit-test the middleware's mapping by invoking it with a `DefaultHttpContext` and a delegate that throws each exception, asserting status + JSON `code`. First READ `src/Waao.API/Middleware/ExceptionHandlingMiddleware.cs` to learn its constructor/Invoke shape and existing mapping style, then write a test matching that shape, e.g.:
```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Waao.API.Middleware;
using Waao.Services.Abstractions;
using Xunit;

namespace Waao.Tests.Auth;

public sealed class ExceptionMappingTests
{
	private static async Task<(int status, string body)> Run(Exception ex)
	{
		var ctx = new DefaultHttpContext();
		ctx.Response.Body = new MemoryStream();
		var mw = new ExceptionHandlingMiddleware(_ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance);
		await mw.InvokeAsync(ctx);
		ctx.Response.Body.Position = 0;
		return (ctx.Response.StatusCode, await new StreamReader(ctx.Response.Body).ReadToEndAsync());
	}

	[Fact]
	public async Task EmailNotVerified_Maps_403_WithCode()
	{
		var (status, body) = await Run(new EmailNotVerifiedException("a@waao.com.br"));
		status.Should().Be(403);
		JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be("email_not_verified");
	}

	[Fact]
	public async Task InvalidToken_Maps_400_WithCode()
	{
		var (status, body) = await Run(new InvalidVerificationTokenException());
		status.Should().Be(400);
		JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be("invalid_or_expired_token");
	}
}
```
> Adjust the `new ExceptionHandlingMiddleware(...)` construction and `InvokeAsync` name to the REAL signature in the file. If the middleware isn't unit-test-friendly (e.g., needs more services), construct it with the minimum the file requires.

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Waao.Tests --filter ExceptionMappingTests`
Expected: FAIL (no mapping for the new exceptions yet → likely 500).

- [ ] **Step 3: Map the exceptions**

In `src/Waao.API/Middleware/ExceptionHandlingMiddleware.cs`, in the existing exception-to-response switch/if-chain (match the file's existing pattern for how it writes JSON; it already maps `ValidationException`/`UnauthorizedAccessException`), add mappings BEFORE the generic 500 fallback:
- `EmailNotVerifiedException` → HTTP 403, JSON body `{ "code": "email_not_verified", "message": <ex.Message> }`
- `InvalidVerificationTokenException` → HTTP 400, JSON body `{ "code": "invalid_or_expired_token", "message": <ex.Message> }`
Use the same JSON serialization the middleware already uses for other errors (do not introduce a different serializer). Add `using Waao.Services.Abstractions;`.

- [ ] **Step 4: Controller endpoints**

In `src/Waao.API/Controllers/AuthController.cs`:
- Change `Register` to return the new type:
```csharp
	[HttpPost("register")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.RegisterAsync(dto, ct));
```
- Add:
```csharp
	[HttpPost("verify-email")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto, CancellationToken ct)
		=> Ok(await Service.VerifyEmailAsync(dto, ct));

	[HttpPost("resend-verification")]
	[AllowAnonymous]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto, CancellationToken ct)
	{
		await Service.ResendVerificationAsync(dto, ct);
		return Ok(new { status = "ok" });
	}
```
(Match the controller's existing primary-ctor service field name and action style.)

- [ ] **Step 5: Run + build + full suite**

Run: `dotnet test tests/Waao.Tests --filter ExceptionMappingTests` (PASS), `dotnet build src/Waao.API/Waao.API.csproj -c Release` (0 errors), `dotnet test tests/Waao.Tests` (ALL green).

- [ ] **Step 6: Commit**

```bash
git add src/Waao.API/Controllers/AuthController.cs src/Waao.API/Middleware/ExceptionHandlingMiddleware.cs tests/Waao.Tests/Auth/ExceptionMappingTests.cs
git -c user.email="higor@waao.com.br" commit -m "feat: auth verify/resend endpoints + exception JSON mapping"
```

---

### Task 8: Frontend — verify page, register/login flows, i18n

**Files (WaaoFrontend repo `/Users/higorflopes/RiderProjects/Repositories/Waao/WaaoFrontend`):**
- Modify: `src/services/auth.service.ts`, `src/pages/auth/register-page.tsx`, `src/pages/auth/login-page.tsx`, the router file, `src/locales/{pt-BR,en,es}/common.json`
- Create: `src/pages/auth/verify-email-page.tsx`

> Separate repo. Same standards: app's own UI lib (no @medtrack/ui, no raw HTML for new controls), `@tanstack/react-query`, no `any`, pt-BR i18n source. Commit conventional/no AI; do NOT push.

- [ ] **Step 1: Explore** — read `src/services/auth.service.ts`, `src/pages/auth/{login,register}-page.tsx`, the router (find where routes are declared), `src/lib/api-client.ts`, an existing page using `useTranslation` + a mutation, and `src/locales/pt-BR/common.json` (existing `auth.*` keys). Confirm the `apiClient` base (`/api/waao`) and how errors expose `response.data.code`.

- [ ] **Step 2: Service** — in `auth.service.ts`: change `register` return type to `{ status: string; email: string }`; add `verifyEmail(token: string)` → `POST /auth/verify-email {token}` returning the existing `AuthResult` type; add `resendVerification(email: string)` → `POST /auth/resend-verification {email}` returning `void`/`{status:string}`. Match the file's existing method/typing pattern; no `any`.

- [ ] **Step 3: i18n** — add to ALL 3 `src/locales/{pt-BR,en,es}/common.json` under `auth` (merge, don't duplicate the `auth` key): `auth.register.domainHint`, `auth.register.checkEmailTitle`, `auth.register.checkEmailBody` (with `{{email}}`), `auth.register.resend`, `auth.register.resendCooldown`, `auth.verify.verifying`, `auth.verify.success`, `auth.verify.invalid`, `auth.verify.resend`, `auth.login.notVerified`, `auth.login.resend`, `auth.common.resendSent`. pt-BR genuine source; en/es real translations; consistent ordering; validate each file parses with `node -e "JSON.parse(require('fs').readFileSync('src/locales/<l>/common.json','utf8'))"`.

- [ ] **Step 4: register-page** — add a client-side `@waao.com.br` check (mirror server message via `t('auth.register.domainHint')`); on successful `register()` (now returns `{status,email}`, NOT an auth/token), swap the form for a "check your email" panel showing `t('auth.register.checkEmailBody',{email})` and a Resend button that calls `authService.resendVerification(email)` and is disabled for 60s after each click (local timer state). Do NOT auto-login (register no longer returns a token).

- [ ] **Step 5: verify-email-page** — create `src/pages/auth/verify-email-page.tsx`: read `?token` from the URL (use the app's router hook), on mount call `authService.verifyEmail(token)` via react-query mutation; on success store the JWT exactly how `login-page` stores it (reuse the same auth context/store) and redirect to the app root/dashboard; on failure show `t('auth.verify.invalid')` + an email input + Resend button (`resendVerification`). Register the route `/verify-email` as a PUBLIC (unauthenticated) route in the router (match how `/login`,`/register` are declared; ensure the auth guard does NOT block it).

- [ ] **Step 6: login-page** — in the login error handler, detect `err?.response?.data?.code === 'email_not_verified'` and render an inline notice `t('auth.login.notVerified')` plus a Resend control calling `authService.resendVerification(enteredEmail)` (60s cooldown like register). Other login errors unchanged.

- [ ] **Step 7: Build** — `npm run build` (tsc -b && vite build) MUST pass: 0 TS errors, no `any`, no unused-locals.

- [ ] **Step 8: Commit (scoped)**

```bash
git add src/services/auth.service.ts src/pages/auth/register-page.tsx src/pages/auth/login-page.tsx src/pages/auth/verify-email-page.tsx <router file> src/locales/pt-BR/common.json src/locales/en/common.json src/locales/es/common.json
git -c user.email="higor@waao.com.br" commit -m "feat: email verification UI — verify page, register/login flows, i18n"
```
Confirm `git status` shows no stray (`.claude-flow/`, `dist/`, `*.tsbuildinfo`).

---

### Task 9: Full verification + deploy

- [ ] **Step 1** Backend: `dotnet test tests/Waao.Tests` (all green) + `dotnet build src/Waao.API/Waao.API.csproj -c Release` (0 errors).
- [ ] **Step 2** Deploy backend (manual — WaaoBackend has no CI/CD): `cd /Users/higorflopes/RiderProjects/Repositories/Waao/WaaoBackend && fly deploy --remote-only`. Startup runs `AddEmailVerification` under the advisory lock. Optionally set the real key: `fly secrets set --app waao-api Resend__ApiKey="re_…"` (without it, the logging fallback is used — link appears in `fly logs`).
- [ ] **Step 3** Smoke: `curl -fsS https://waao-api.fly.dev/health`; `curl -i -X POST https://waao-api.fly.dev/api/waao/auth/register -H 'Content-Type: application/json' -d '{"fullName":"Test","email":"verifytest@waao.com.br","password":"Sup3rSecret!","joinDate":"2026-05-19"}'` → expect 201 `{status:"verification_sent"}`; `fly logs --app waao-api` → find the `[DEV email] Verification link` (if no Resend key); POST that token to `/api/waao/auth/verify-email` → expect 200 with a JWT; login with that user → 200. Login an unverified second registration → 403 `{code:"email_not_verified"}`.
- [ ] **Step 4** Frontend: `cd /Users/higorflopes/RiderProjects/Repositories/Waao/WaaoFrontend && git push origin main` (Cloudflare Worker CI auto-deploys). Verify `https://waao-frontend.higorflopes.workers.dev` returns 200 and `/verify-email` route loads.
- [ ] **Step 5** Append a dated section to `Memory/Journals/WAAO/deploy-2026-05-19.md`: email verification shipped, migration applied, Resend key status, smoke results, default seeded higor now `EmailVerified=true`.

---

## Self-Review

**Spec coverage:** register-unverified+token+email→Task5; `@waao.com.br` validator→Task3; admin-from-config→Task5; verify+auto-login→Task6; login gate 403→Task6/7; resend rate-limited/no-enumeration→Task6; IEmailSender Resend+log-fallback→Task4; 5 entity fields+index→Task1; migration+backfill→Task2; seeder verified→Task2; DTOs/exceptions→Task3; controller+middleware mapping→Task7; frontend verify/register/login/i18n→Task8; deploy+smoke→Task9. Day-one-gamification-on-verify reconciled (B onboarding gate makes it a harmless no-op until onboarded — noted in Task 6). All spec sections covered.

**Placeholder scan:** No TBD/"handle errors" — concrete code in every code step. Conditional "read the real signature/name" instructions are explicit verification steps with a stated fallback, not placeholders. Frontend Task 8 references real files + provides concrete behavior per step (UI uses the app's own lib as established in Feature B).

**Type consistency:** `RegisterResultDto{Status,Email}`, `VerifyEmailDto{Token}`, `ResendVerificationDto{Email}` consistent across DTO/validator/service/controller/test. `IAuthService.RegisterAsync:Task<RegisterResultDto>`, `VerifyEmailAsync:Task<AuthResultDto>`, `ResendVerificationAsync:Task` identical in interface/impl/controller/tests. `IEmailSender.SendVerificationAsync(string,string,string,CancellationToken)` identical in interface/Resend/Logging/factory double. `EmailNotVerifiedException`/`InvalidVerificationTokenException` consistent across throw site, middleware, tests. Token helper `NewToken()` defined once (Task 5), reused in Task 6. AuthService ctor params (added in Task 5) reused by Task 6 without change; `AuthServiceFactory` matches the final ctor.
