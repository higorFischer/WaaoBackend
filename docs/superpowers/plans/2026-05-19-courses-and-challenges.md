# Courses + Challenges (E + F) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Feature E (Courses) and Feature F (Challenges) end-to-end — backend + frontend + i18n + nav wiring + massive seed (30+ courses, 20+ challenges) — in a single subagent-driven sweep.

**Architecture:**
- Backend: two new modules `Courses` and `Challenges`, each with entities, DTOs, validators, services, controllers; one EF migration `AddCoursesAndChallenges` covering both.
- Frontend: new routes `/courses` + `/challenges` with browse/detail/take/me pages, admin CRUD + review queues, full i18n × 3 locales, sidebar + CommandPalette wiring.
- Seed: `DbInitializer.SeedDefaultCoursesAsync` + `SeedDefaultChallengesAsync` invoked on startup (idempotent, only seeds when respective tables are empty).
- Both features feed `SkillRadarCard` through `XpTransaction.reason` text — no radar refactor.

**Tech Stack:** .NET 9, EF Core 9 (Npgsql), FluentValidation, React 19 + TypeScript + Vite, i18next, TanStack Query, RHF + Zod.

**References (golden examples):**
- Backend module: `src/Waao.API/Controllers/AdminController.cs`, `src/Waao.Services/Services/AdminService.cs`, `src/Waao.Infra.EF/Configurations/*`
- Migration pattern: `20260519190724_ManualXpEconomyReset.cs`, `20260519200355_AddEmailVerification.cs`
- XP grant via `GamificationEngine.RecordAsync`: see `AdminService.GrantXpAsync`
- Frontend page: any existing `src/pages/*` (use the simplest existing CRUD page as template)
- SkillRadar matcher (no change): `src/components/charts/skill-radar.tsx` + `src/pages/dashboard/components/skill-radar-card.tsx`

---

## Task 1: Backend domain models + EF configuration

**Files:**
- Create: `src/Waao.Domain.Models/Entities/Course.cs`
- Create: `src/Waao.Domain.Models/Entities/CourseCompletion.cs`
- Create: `src/Waao.Domain.Models/Entities/Challenge.cs`
- Create: `src/Waao.Domain.Models/Entities/ChallengeQuestion.cs`
- Create: `src/Waao.Domain.Models/Entities/ChallengeAttempt.cs`
- Create: `src/Waao.Domain.Models/Entities/ChallengeAttemptAnswer.cs`
- Create: `src/Waao.Infra.EF/Configurations/CourseConfiguration.cs`
- Create: `src/Waao.Infra.EF/Configurations/CourseCompletionConfiguration.cs`
- Create: `src/Waao.Infra.EF/Configurations/ChallengeConfiguration.cs`
- Create: `src/Waao.Infra.EF/Configurations/ChallengeQuestionConfiguration.cs`
- Create: `src/Waao.Infra.EF/Configurations/ChallengeAttemptConfiguration.cs`
- Create: `src/Waao.Infra.EF/Configurations/ChallengeAttemptAnswerConfiguration.cs`
- Modify: `src/Waao.Infra.EF/WaaoDbContext.cs` (add `DbSet`s + `OnModelCreating` apply)

Steps:
- [ ] Implement all six entities matching the specs in `2026-05-19-courses-design.md` and `2026-05-19-challenges-design.md`. Inherit from existing entity base (see `Collaborator`).
- [ ] Implement configurations with snake_case table/column mapping, soft-delete query filter (`HasQueryFilter(e => !e.IsDeleted)`), indexes per spec.
- [ ] Wire `DbSet`s in `WaaoDbContext` and call `ApplyConfiguration`.
- [ ] `dotnet build src/Waao.API/Waao.API.csproj` — must succeed.
- [ ] Commit: `feat(courses,challenges): domain entities + EF configurations`

## Task 2: Migration

**Files:** Create via `dotnet ef migrations add AddCoursesAndChallenges -p Waao.Infra.EF -s Waao.API`

Steps:
- [ ] Run the EF migrations command from `WaaoBackend/src`.
- [ ] Review generated SQL — should be additive only (no drops, no modifications to existing tables).
- [ ] Apply locally: `dotnet ef database update -p Waao.Infra.EF -s Waao.API` against `WaaoLocal`.
- [ ] Verify with psql: tables `courses`, `course_completions`, `challenges`, `challenge_questions`, `challenge_attempts`, `challenge_attempt_answers` exist with snake_case columns.
- [ ] Commit: `feat(courses,challenges): add migration AddCoursesAndChallenges`

## Task 3: DTOs

**Files:**
- Create: `src/Waao.Services.Abstractions/Dtos/Courses/{CourseDto,CreateCourseDto,UpdateCourseDto,CourseListFilterDto,CourseCompletionDto,MarkCourseCompleteDto,GrantCourseXpDto}.cs`
- Create: `src/Waao.Services.Abstractions/Dtos/Challenges/{ChallengeDto,PublicChallengeDto,CreateChallengeDto,UpdateChallengeDto,ChallengeQuestionDto,PublicChallengeQuestionDto,CreateChallengeQuestionDto,UpdateChallengeQuestionDto,ChallengeAttemptDto,SubmitChallengeAttemptDto,ChallengeAttemptResultDto,GrantChallengeXpDto}.cs`

All DTOs as `record` with `init`. Build must succeed. Commit.

## Task 4: Validators

**Files:** Create validators in `src/Waao.Services/Validation/{Courses,Challenges}/*Validator.cs` per spec. Register in DI (`Program.cs`). Commit.

## Task 5: ICourseService + CourseService

**Files:**
- Create: `src/Waao.Services.Abstractions/Services/ICourseService.cs`
- Create: `src/Waao.Services.Abstractions/Services/ICourseCompletionService.cs`
- Create: `src/Waao.Services/Services/CourseService.cs`
- Create: `src/Waao.Services/Services/CourseCompletionService.cs`

Match the spec's method surface. GrantXpForCompletionAsync calls `GamificationEngine.RecordAsync(..., XpSource.Admin, $"Course completed: {course.Title} [Category: {course.Category}]", completionId, "CourseCompletion", ct)`. Idempotent. Soft-delete only. Commit.

## Task 6: IChallengeService + IChallengeAttemptService + impls

**Files:** Mirror structure under `Services/Services`. The submit grading does `correctCount / totalCount` for score, sets `Passed = scorePct >= challenge.PassPercent`. Public DTOs strip `CorrectOption`. Grant-xp emits `XpTransaction` with reason `Challenge passed: {Title} [Category: {Category}] ({ScorePct}%)` and `sourceEntityType = "ChallengeAttempt"`. Commit.

## Task 7: Controllers

**Files:**
- Create: `src/Waao.API/Controllers/CoursesController.cs`
- Create: `src/Waao.API/Controllers/ChallengesController.cs`
- Modify: `src/Waao.API/Controllers/AdminController.cs` — add `course-completions/{pending,grant-xp}` and `challenge-attempts/{pending,grant-xp}` endpoints

One-line expression-bodied methods. `[Authorize]` class-level; `[Authorize(Policy = "HR")]` on CRUD; Admin on delete + grant-xp. `Guid id` with `{id:guid}` constraint. `[ProducesResponseType]` on every action. Commit.

## Task 8: DI registration

**Files:** Modify `src/Waao.API/Program.cs` — register all new services as Scoped. Build must succeed. Commit.

## Task 9: Seed data

**Files:**
- Modify: `src/Waao.Infra.EF/DbInitializer.cs` — add `SeedDefaultCoursesAsync(ct)` and `SeedDefaultChallengesAsync(ct)`, call from existing `SeedAsync`.

Content: 30 published courses spread across the 6 SkillRadar axes (Backend, Frontend, DevOps, Quality, Communication, Leadership) with realistic WAAO-flavored titles. 20 published challenges (5 questions each, 4 options, correct option marked) covering the same axes. All idempotent: only seed when respective tables are empty.

Author = first Admin (`higor@waao.com.br`). `IsPublished = true` so they show in the catalog immediately.

Commit: `feat(seed): 30 courses + 20 challenges across all skill axes`

## Task 10: Frontend types + services

**Files:**
- Modify: `WaaoFrontend/src/types/waao.types.ts` — add `Course`, `CourseCompletion`, `Challenge`, `ChallengeQuestion`, `ChallengeAttempt`, etc. (camelCase, no `any`).
- Create: `WaaoFrontend/src/services/courses.service.ts`
- Create: `WaaoFrontend/src/services/challenges.service.ts`
- Modify: `WaaoFrontend/src/services/admin.service.ts` — add `listPendingCourseCompletions`, `grantCourseXp`, `listPendingChallengeAttempts`, `grantChallengeXp`.

Commit.

## Task 11: Courses frontend pages + route

**Files:**
- Create: `WaaoFrontend/src/pages/courses/CoursesPage.tsx`
- Create: `WaaoFrontend/src/pages/courses/CourseDetailPage.tsx`
- Create: `WaaoFrontend/src/pages/courses/MyCoursesPage.tsx`
- Create: `WaaoFrontend/src/pages/admin/courses/AdminCoursesPage.tsx`
- Create: `WaaoFrontend/src/pages/admin/courses/AdminCourseReviewPage.tsx`
- Modify: `WaaoFrontend/src/App.tsx` (or wherever routes live) — add `/courses`, `/courses/:id`, `/me/courses`, `/admin/courses`, `/admin/courses/review`.

TanStack Query for fetches. Own UI lib (NOT @medtrack/ui). All text via `t()`. Commit.

## Task 12: Challenges frontend pages + route

**Files:** Mirror Courses structure under `WaaoFrontend/src/pages/challenges` and `admin/challenges`. Add `ChallengeAttemptPage` that walks questions and submits answers. Commit.

## Task 13: i18n keys × 3 locales

**Files:**
- Modify: `WaaoFrontend/src/locales/pt-BR/common.json`
- Modify: `WaaoFrontend/src/locales/en/common.json`
- Modify: `WaaoFrontend/src/locales/es/common.json`

Add full `courses.*` and `challenges.*` namespaces. pt-BR is canonical; en/es are genuine translations (not copies). Commit.

## Task 14: Sidebar + CommandPalette + nav

**Files:**
- Modify: `WaaoFrontend/src/components/layout/Sidebar.tsx` (or equivalent) — point Courses nav at `/courses` and Challenges nav at `/challenges` (remove coming-soon).
- Modify: `WaaoFrontend/src/components/layout/CommandPalette.tsx` — add courses + challenges entries.

Commit.

## Task 15: Build + deploy

Steps:
- [ ] `dotnet build src/Waao.API/Waao.API.csproj` — clean.
- [ ] `npm run build` in WaaoFrontend — clean.
- [ ] Commit anything pending.
- [ ] Backend: `git push origin main` from `WaaoBackend/` — CI/CD will deploy (the workflow we just added).
- [ ] Frontend: `git push origin main` from `WaaoFrontend/` — Cloudflare Worker auto-deploys.
- [ ] Wait for both, smoke-test https://waao-api.fly.dev/health and the frontend.

---

## Conventions reminder (every task obeys)

- TABS indentation (4) in C# code; file-scoped namespaces; primary-constructor DI with PascalCase params.
- DTOs as `record` with `init`. NO data annotations — FluentValidation only.
- `Guid.CreateVersion7()` everywhere (existing helper).
- `DateTime.UtcNow` everywhere.
- Soft delete only.
- Frontend: NO `any`, all text via `t()`, NO raw HTML — use the WAAO UI lib.
- Commit prefixes only: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`. NO Claude/AI/Co-Authored-By.
- Commit author: `higor@waao.com.br` (set via `git -c user.email="higor@waao.com.br" commit ...` if global is different).
