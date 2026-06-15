# Team Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give managers and Admin/HR a place to track how each team member is doing (current work, skills, performance, 1:1s) via a Management tab on the collaborator detail page, plus an Admin team-wide overview.

**Architecture:** Reuse existing WAAO features (Allocation, OneOnOnes, Feedback, CareerEvents) and add only the gaps: a Skills matrix, private Manager Notes, a `my-team` endpoint, and a `ManagerAccess` authorization helper. Frontend aggregates everything into one manager-only tab + a roster overview page.

**Tech Stack:** .NET 9 (Waao.* clean architecture), EF Core + PostgreSQL, React 19 + TypeScript + Vite, TanStack Query, Zustand (`use-auth`), WAAO MCP scaffolding tools.

**Spec:** `Waao/WaaoBackend/docs/superpowers/specs/2026-06-15-team-management-design.md`

---

## Pre-flight (do first)

- [ ] **P1.** Create an isolated worktree per `superpowers:using-git-worktrees` (backend + frontend are separate git repos — make one per repo, branch `feat/team-management`).
- [ ] **P2.** Confirm infra names: run `waao_golden_example` for a CareerEvent-like entity and `waao_modules` to confirm the DbContext project (`Waao.Infra.EF`), DbContext class name, and the `dotnet ef` migration invocation. Record them here before coding.
- [ ] **P3.** Confirm the `[Authorize(Policy="HR")]` / `Policy="Admin"` policies exist (seen on CareerEvents/Allocations controllers) and how `RoleKind` maps to them.

---

## Phase 1 — Backend

### Task 1: SkillLevel enum

**Files:**
- Create: `src/Waao.Domain.Models/Enums/SkillLevel.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace Waao.Domain.Models.Enums;

public enum SkillLevel
{
	Novice = 1,
	Beginner = 2,
	Competent = 3,
	Proficient = 4,
	Expert = 5,
}
```

- [ ] **Step 2: Commit** — `git commit -m "feat: add SkillLevel enum"`

### Task 2: Skill + CollaboratorSkill + ManagerNote entities

**Files:**
- Create: `src/Waao.Domain.Models/Entities/Skills/Skill.cs`
- Create: `src/Waao.Domain.Models/Entities/Skills/CollaboratorSkill.cs`
- Create: `src/Waao.Domain.Models/Entities/Team/ManagerNote.cs`

- [ ] **Step 1: Skill** (tenant-scoped catalog — match the tenant pattern used by `Project`)

```csharp
using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Skills;

public class Skill : Entity
{
	public Guid? TenantId { get; set; }
	public virtual Tenant? Tenant { get; set; }

	public string Name { get; set; } = string.Empty;
	public string? Category { get; set; }
	public bool IsArchived { get; set; }

	public virtual ICollection<CollaboratorSkill> CollaboratorSkills { get; set; } = [];
}
```

- [ ] **Step 2: CollaboratorSkill**

```csharp
using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Skills;

public class CollaboratorSkill : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator? Collaborator { get; set; }

	public Guid SkillId { get; set; }
	public virtual Skill? Skill { get; set; }

	public SkillLevel Level { get; set; } = SkillLevel.Competent;
	public string? Note { get; set; }

	public Guid AssessedById { get; set; }
	public DateTime AssessedAt { get; set; }
}
```

- [ ] **Step 3: ManagerNote** (private review log — never shown to the subject)

```csharp
namespace Waao.Domain.Models.Entities.Team;

public class ManagerNote : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator? Collaborator { get; set; }

	public Guid AuthorId { get; set; }
	public string AuthorName { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;
	public bool Pinned { get; set; }
}
```

- [ ] **Step 4: Commit** — `git commit -m "feat: add Skill, CollaboratorSkill, ManagerNote entities"`

### Task 3: DbContext config + migration

**Files:**
- Modify: the Waao DbContext (confirmed in P2) — add `DbSet`s + entity configs.

- [ ] **Step 1:** Register `DbSet<Skill>`, `DbSet<CollaboratorSkill>`, `DbSet<ManagerNote>`.
- [ ] **Step 2:** Configure: snake_case (auto), `HasQueryFilter(!IsDeleted)` (+ tenant filter on `Skill` matching `Project`), enum `Level` stored as string via `HasConversion<string>()`, and a **unique index** on `CollaboratorSkill (CollaboratorId, SkillId)` filtered `WHERE is_deleted = false`.
- [ ] **Step 3:** Create migration via `waao_migrations_add` (name `AddTeamManagement`). If the MCP tool is unavailable, use `dotnet ef migrations add AddTeamManagement -p Waao.Infra.EF -s Waao.API`.
- [ ] **Step 4: REVIEW the migration** — confirm 3 additive tables only, no changes to existing tables, defaults on non-nullable columns, filtered unique index present. Per `migration-reviewer`.
- [ ] **Step 5:** Apply to **dev DB** via `waao_migrations_apply` (or `dotnet ef database update`). Verify tables exist.
- [ ] **Step 6: Commit** — `git commit -m "feat: add team-management migration"`

### Task 4: ManagerAccess authorization helper (TDD — this is the security-critical unit)

**Files:**
- Create: `src/Waao.Services/Services/Team/ManagerAccess.cs`
- Test: `tests/.../Team/ManagerAccessTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// caller is HR/Admin → access to anyone
// caller is the target's manager (target.ManagerId == caller.Id) → access
// caller is a peer (no manager rel, not HR/Admin) → NO access
// caller viewing self → access to skills, but NOT to manager notes (separate method)
[Fact] public void HrCanAccessAnyone() { ... Assert.True(ManagerAccess.CanManage(hr, target)); }
[Fact] public void ManagerCanAccessDirectReport() { target.ManagerId = mgr.Id; Assert.True(ManagerAccess.CanManage(mgr, target)); }
[Fact] public void PeerCannotAccess() { Assert.False(ManagerAccess.CanManage(peer, target)); }
[Fact] public void SubjectCannotReadOwnManagerNotes() { Assert.False(ManagerAccess.CanReadManagerNotes(self, self)); }
```

- [ ] **Step 2: Run tests → FAIL** (`ManagerAccess` not defined).
- [ ] **Step 3: Implement**

```csharp
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Enums;

namespace Waao.Services.Team;

public static class ManagerAccess
{
	public static bool IsStaff(Collaborator caller)
		=> caller.RoleKind is CollaboratorRoleKind.HR or CollaboratorRoleKind.Admin;

	public static bool CanManage(Collaborator caller, Collaborator target)
		=> IsStaff(caller) || target.ManagerId == caller.Id;

	// Private review material: staff or the managing manager, but NEVER the subject.
	public static bool CanReadManagerNotes(Collaborator caller, Collaborator target)
		=> caller.Id != target.Id && CanManage(caller, target);
}
```

- [ ] **Step 4: Run tests → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat: add ManagerAccess authorization helper with tests"`

### Task 5: Skills service + controller

**Files:**
- Create: DTOs `src/Waao.Services.Abstractions/Dtos/Skills/*` (records, `init`), validators `src/Waao.Services/Validation/Skills/*`, mappers `src/Waao.Services/Mappers/Skills/*`, `ISkillService`, `SkillService`, `SkillsController`.

- [ ] **Step 1:** Prefer `waao_scaffold_crud` for the `Skill` catalog (tenant-scoped). Then hand-add the `CollaboratorSkill` assessment endpoints:
  - `GET /skills` (catalog), `POST/PUT/DELETE /skills` (Admin/HR).
  - `GET /collaborators/{id}/skills` — list a person's assessed skills. Guard: `CanManage` OR `caller.Id == id` (people see own skills).
  - `PUT /collaborators/{id}/skills/{skillId}` — upsert level+note. Guard: `CanManage`. Sets `AssessedById`, `AssessedAt = DateTime.UtcNow`.
  - `DELETE /collaborators/{id}/skills/{skillId}` — soft delete. Guard: `CanManage`.
- [ ] **Step 2:** Controller = one-line expression-bodied methods, `IActionResult`, `[ProducesResponseType]`, `{id:guid}`. All authz in the service via `ManagerAccess` + the current-user accessor.
- [ ] **Step 3:** Build `Waao.API` (`dotnet build`). Expected: success.
- [ ] **Step 4: Commit** — `git commit -m "feat: skills matrix service + endpoints"`

### Task 6: Manager notes service + controller

**Files:**
- Create: `ManagerNote` DTOs/validator/mapper, `IManagerNoteService`, `ManagerNoteService`, `ManagerNotesController`.

- [ ] **Step 1:** Endpoints:
  - `GET /collaborators/{id}/manager-notes` — Guard: `CanReadManagerNotes` (staff or managing manager; **never** the subject → 403).
  - `POST /collaborators/{id}/manager-notes` — create (Body, Pinned). Guard: `CanReadManagerNotes`. Sets `AuthorId`/`AuthorName` from current user.
  - `PUT /manager-notes/{id}` / `DELETE /manager-notes/{id}` — author or staff only.
- [ ] **Step 2:** Build `Waao.API`. Expected: success.
- [ ] **Step 3: Commit** — `git commit -m "feat: private manager notes service + endpoints"`

### Task 7: my-team endpoint

**Files:**
- Modify: `CollaboratorsController` + `CollaboratorService` (+ abstraction).

- [ ] **Step 1:** Add `GET /collaborators/my-team` → caller's direct reports (`ManagerId == caller.Id`). For HR/Admin, support `?all=true` to return everyone (for the overview page).
- [ ] **Step 2:** Return a lightweight `TeamMemberSummaryDto` (id, fullName, photoUrl, roleTitle, status, current allocation count, skill count, lastOneOnOneDate). Compose from existing services — do not cross DbContext boundaries improperly; reuse repositories.
- [ ] **Step 3:** Build + commit — `git commit -m "feat: add my-team endpoint with team-member summaries"`

### Task 8: Backend validation gate

- [ ] **Step 1:** Run `waao_validate_changed` (or `waao_validate`) → fix any standards violations (TABS, file-scoped namespaces, record DTOs, enum-as-string, no `Guid.NewGuid()`/`DateTime.Now`).
- [ ] **Step 2:** Run backend tests → all pass. Commit any fixes.

---

## Phase 2 — Frontend

### Task 9: Types + services

**Files:**
- Create: `src/types/team.types.ts` (Skill, CollaboratorSkill, SkillLevel, ManagerNote, TeamMemberSummary — no `any`).
- Create: `src/services/skills.service.ts`, `src/services/manager-notes.service.ts`. Extend `collaborators.service.ts` with `myTeam(all?)`.
- [ ] Match the existing service class pattern (`private get api() { return apiClient.<x> }`, exported singleton). Commit.

### Task 10: Tabs primitive

**Files:**
- Create: `src/components/ui/tabs.tsx` (Radix `@radix-ui/react-tabs` wrapper) — none exists today. WAI-ARIA, kebab-case file, PascalCase export.
- [ ] If `@radix-ui/react-tabs` isn't installed, `npm i @radix-ui/react-tabs`. Commit.

### Task 11: Management tab on collaborator detail page

**Files:**
- Modify: `src/pages/collaborators/collaborator-detail-page.tsx`
- Create: `src/pages/collaborators/management/` → `skills-matrix.tsx`, `manager-notes.tsx`, `current-work-panel.tsx` (embeds allocation), `management-tab.tsx`.

- [ ] **Step 1:** Compute gate: `const canManage = me?.roleKind === 'Admin' || me?.roleKind === 'HR' || me?.isSuperAdmin || target.managerId === me?.id;`
- [ ] **Step 2:** Wrap the page body in `<Tabs>`: "Overview" (existing content) + a `canManage`-only **"Management"** tab rendering `<ManagementTab collaborator={target} />`.
- [ ] **Step 3:** `ManagementTab` lays out 4 panels: Current work (allocation embed) · Skills matrix (editable, level 1–5 select + note) · Performance (existing career-events/feedback embeds + ManagerNotes editor) · 1:1s (existing embed). Manager notes hidden when `target.id === me.id`.
- [ ] **Step 4:** All data via TanStack Query with descriptive keys (`['collaborator', id, 'skills']`, etc.). Commit.

### Task 12: Admin Team Overview page

**Files:**
- Create: `src/pages/team/team-overview-page.tsx`
- Modify: `src/App.tsx` (route, HR/Admin guard), `src/components/layout/sidebar.tsx` (nav item, role-gated), command palette registration.

- [ ] **Step 1:** Page calls `collaboratorsService.myTeam(true)`; renders a roster table: person · role · current work · skill coverage · last 1:1 · status. Row click → collaborator detail (Management tab).
- [ ] **Step 2:** Register the page in **both** sidebar + command palette (use `waao_register_page` / `waao_nav_check` to verify). Route gated to HR/Admin.
- [ ] **Step 3:** Commit.

### Task 13: i18n + version bump

- [ ] **Step 1:** Add all new keys to **all 3 locales** (pt-BR, en, es) via `waao_i18n`. Namespace `team`. Verify with `waao_i18n` / `i18n-check`.
- [ ] **Step 2:** Bump `WaaoFrontend/package.json` `1.26.1 → 1.27.0`.
- [ ] **Step 3:** `npm run build` → success. Commit — `git commit -m "feat: team management frontend (skills, manager notes, overview)"`.

---

## Phase 3 — Validate & ship

- [ ] **V1.** `waao_validate_changed` on the combined diff (both repos) → green.
- [ ] **V2.** Backend `dotnet build` + tests green; frontend `npm run build` green.
- [ ] **V3.** Manual smoke (dev): as Admin see a report's Management tab + skills + a manager note; confirm the subject CANNOT see their own manager notes (403); confirm a peer gets no Management tab; Team Overview lists the roster.
- [ ] **V4.** Ship: push backend (Fly auto-deploys), push frontend then **force-deploy the Worker** (`npm run deploy` — CF git auto-deploy does not work). Apply the migration to prod via `waao_migrations_apply` per deploy runbook.
- [ ] **V5.** Update the journal/memory; mark the spec status `Shipped`.

---

## Self-review notes
- **Spec coverage:** Access model → Task 4 (helper) + per-endpoint guards (5,6,7); Skills → 1,2,3,5,9,11; Manager notes → 2,3,6,9,11; reuse of allocation/1:1/feedback → embeds in Task 11; Admin overview ("track how my workers are going") → Task 7 + Task 12; privacy (subject can't read own notes) → Task 4 `CanReadManagerNotes` + Task 6 guard + Task 11 UI hide. All covered.
- **Type consistency:** `ManagerAccess.CanManage` / `CanReadManagerNotes`, `SkillLevel` (1–5), `TeamMemberSummaryDto`/`TeamMemberSummary` used consistently across backend/frontend.
- **Risk:** the only schema change is 3 additive tables; the security-critical logic is unit-tested (Task 4). Migration reviewed before apply.
