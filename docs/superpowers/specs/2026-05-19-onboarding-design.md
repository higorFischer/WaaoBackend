# WAAO — Onboarding wizard + gamification gating

- **Date:** 2026-05-19
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (status/complete endpoints), `WaaoFrontend` (wizard + banner)
- **Module:** Auth / Collaborator / Gamification (gate) / Frontend
- **Sequence:** Feature **D**, after B and C. Revised roadmap: **B → C → D → A**.

## Goal

New collaborators go through a guided onboarding wizard (welcome screens + a
required profile step) after verifying their email. Until onboarding is complete
they can still browse the app but see a persistent "finish onboarding" banner,
and **all automatic gamification (badges, streaks, career-event passes) is
suppressed**. Completing onboarding activates gamification.

## Locked decisions (from brainstorming)

| # | Decision |
|---|----------|
| Scope | Guided multi-step wizard: welcome/intro screen(s) **and** a required profile step. |
| Gating | Browse-but-nagged: app usable, persistent banner, gamification off until complete. **No hard redirect.** |
| Required fields | `PhotoUrl`, `Bio`, `Birthdate`, `DepartmentId` all set + welcome screens clicked. (Role/manager remain admin-set later.) |
| 1 | Add `Collaborator.OnboardingCompletedAt` (`DateTime?`). No server-side step tracking — wizard step state is client-only. |
| 2 | Gate centralized: `BadgeEvaluator` + `StreakTracker` early-return when `OnboardingCompletedAt is null`. Career-event rows still recorded. |
| 3 | Fold the **backend gate + column** into Feature B (revise B plan Tasks 3/4/7). Feature D = wizard UI + status/complete endpoints + banner, after B. |
| 4 | Migration backfills existing rows → `now()`; seeded `higor@waao.com.br` → onboarded (bootstrap skips it). |
| 5 | Banner: persistent, per-session dismissible, links to `/onboarding`; not a hard redirect. |
| 6 | "Complete" = the 4 fields valid + wizard finished (server validates the 4 on the complete call). |

## What belongs where (roadmap merge)

**Folded into Feature B** (so `BadgeEvaluator`/`StreakTracker`/migration are
touched once):
- `Collaborator.OnboardingCompletedAt` field — added in B Task 2's entity edit area.
- B Task 3 (BadgeEvaluator): besides removing XP, add
  `if (collaborator.OnboardingCompletedAt is null) return [];` early-return.
- B Task 4 (StreakTracker): besides the 0-XP no-op, early-return the streak
  methods when `OnboardingCompletedAt is null` (no counter changes, returns
  `(0,0,0)`).
- B Task 7 (migration `ManualXpEconomyReset`): also add the
  `onboarding_completed_at` nullable column; backfill non-deleted rows to `now()`;
  seed `higor@waao.com.br` with `OnboardingCompletedAt = UtcNow`.
- B spec updated to note the gate (consistency).

**Feature C** (email-verification spec): narrative updated — `register → verify
email → onboarding → gamification`. No code change beyond that ordering note.

**Feature D (this spec)** — implemented as its own plan after B:
- Backend: `OnboardingController` + service methods + DTOs + validator.
- Frontend: the wizard page/route, the post-login banner, i18n, routing.

## Backend (Feature D portion)

- DTOs (`Waao.Services.Abstractions/Dtos`):
  - `OnboardingStatusDto { bool Completed; DateTime? CompletedAt; bool PhotoSet; bool BioSet; bool BirthdateSet; bool DepartmentSet }`
  - `CompleteOnboardingDto { string PhotoUrl; string Bio; DateOnly Birthdate; Guid DepartmentId }`
- `IOnboardingService` / `OnboardingService`:
  - `GetStatusAsync(Guid collaboratorId, ct)` → reads the collaborator, returns
    `OnboardingStatusDto` (per-field booleans drive the wizard's prefilled state).
  - `CompleteAsync(Guid collaboratorId, CompleteOnboardingDto dto, ct)` →
    validate; set `PhotoUrl/Bio/Birthdate/DepartmentId`; set
    `OnboardingCompletedAt = DateTime.UtcNow`; `SaveChanges`; return status.
    Idempotent: if already completed, return current status without error.
- `CompleteOnboardingValidator` (FluentValidation): `PhotoUrl` not empty,
  `Bio` not empty (≤ 1000), `Birthdate` not default and in the past,
  `DepartmentId` not empty and exists (`Db.Departments.AnyAsync`).
- `OnboardingController` `[Route("api/waao/onboarding")] [Authorize]`:
  - `GET status` → `Ok(GetStatusAsync(Me))`
  - `POST complete` → `Ok(CompleteAsync(Me, dto))` (`Me` = current subject claim,
    same pattern as `AuthController.CurrentCollaboratorId`)
- DI registration in `Program.cs` (`AddScoped<IOnboardingService, OnboardingService>()`).

## Frontend (Feature D portion — `WaaoFrontend`, own UI lib)

- `onboarding.service.ts`: `getStatus()`, `complete(dto)` via `apiClient` `/onboarding/*`.
- New route `/onboarding` (authenticated) → `OnboardingWizard`:
  - Step 1..n: welcome/intro screens (static, i18n copy explaining WAAO Journey,
    badges/streaks, that XP is admin-granted).
  - Final step: profile form — photo (URL/upload field per existing pattern),
    bio (textarea), birthdate (date), department (existing department select).
    RHF + zod, i18n validation, app's own UI components (no `@medtrack/ui`).
  - Submit → `complete()` → on success route to dashboard; gamification now live.
- Post-login: a `useOnboardingStatus` query; if `!completed`, render a persistent
  banner (per-session dismissible via state/localStorage) with a CTA to
  `/onboarding`. Banner lives in the authenticated layout, above page content.
- i18n: all keys in `src/locales/{pt-BR,en,es}/common.json`, pt-BR authored as
  source (aligns with Feature A). New namespace block `"onboarding"`.
- Routing: `/onboarding` added to the router; no guard/redirect (browse allowed).

## Error contract

| Case | HTTP | Body |
|------|------|------|
| `GET status`, authenticated | 200 | `OnboardingStatusDto` |
| `POST complete`, missing/invalid field | 400 | FluentValidation failure |
| `POST complete`, department not found | 400 | validation failure on `departmentId` |
| `POST complete`, already completed | 200 | current `OnboardingStatusDto` (idempotent) |
| Any, unauthenticated | 401 | (auth) |

## Testing

- **Backend unit tests** (`Waao.Tests`, EF InMemory):
  - gate: `BadgeEvaluator.EvaluateAsync` returns empty + writes nothing when
    `OnboardingCompletedAt is null`; works normally once set
  - gate: `StreakTracker.RegisterLoginAsync` no-ops (returns `(0,0,0)`, no field
    changes) when not onboarded; advances once onboarded
  - `CompleteAsync` sets the 4 fields + timestamp; invalid inputs rejected;
    unknown department rejected; second call idempotent (no error, no change)
  - `GetStatusAsync` per-field booleans + completed flag accurate
  - seed: `higor@waao.com.br` seeded with `OnboardingCompletedAt != null`
  - migration: existing non-deleted rows backfilled to non-null
- **Frontend**: manual smoke — new user logs in → banner shows → wizard →
  complete → banner gone, badges/streaks resume; existing/higor user sees no
  banner.

## Rollout / safety

- After Feature B, production holds only `higor@waao.com.br` (seeded onboarded),
  so backfill blast radius is nil. New real signups (post-C) are the first to
  hit the wizard.
- Order strictly **B → C → D → A**. D depends on B's column existing and on C's
  verification preceding onboarding in the UX narrative.
- The gate is fail-closed: if `OnboardingCompletedAt` is null (default for new
  rows), gamification is off — no risk of pre-onboarding XP/badge leakage.
