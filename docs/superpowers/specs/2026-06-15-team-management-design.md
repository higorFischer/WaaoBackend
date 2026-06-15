# Team Management — Design Spec

- **Date:** 2026-06-15
- **Status:** Approved (design)
- **Author:** Higor
- **Scope:** Manager/Admin tooling to track how each team member is doing — current work, skills, performance, 1:1s — plus an admin-wide team overview.

## Goal

Give managers and Admin/HR a place to **write about and track their people** inside WAAO. Most of the data already exists in separate features; this work surfaces it in one manager view and fills two genuine gaps (skills, private manager notes), plus an Admin team-wide tracking dashboard.

## Access model (decided)

- **Per-person management view**: visible/editable by the person's **manager** (`target.ManagerId == caller.Id`) **or** any **HR/Admin**.
- **New data edit rights**: managers edit their direct reports; HR/Admin edit anyone.
- **Privacy**: a collaborator **cannot** see their own *private manager notes* (review material). They can see their own skills.
- **Admin team overview**: HR/Admin only (Admin is the primary audience — "track how my workers are going").

## What already exists (reuse, do not rebuild)

| Dimension | Existing feature | Endpoint |
|---|---|---|
| Current work | Allocation board | `GET /allocations/by-collaborator/{id}` |
| 1:1 log | OneOnOnes (agenda/notes/action items) | `GET /one-on-ones/by-collaborator/{id}` (HR) |
| Performance signals | Feedback, PeerFeedback, CareerEvents (`PerformanceReview`) | existing controllers |

## What's new (the actual build)

### Backend

1. **Skills matrix** (greenfield)
   - `Skill` — tenant-scoped catalog: `Name`, `Category`, `IsArchived`. Admin/HR managed.
   - `CollaboratorSkill` — `CollaboratorId`, `SkillId`, `Level` (1–5 enum `SkillLevel`), `Note?`, `AssessedById`, `AssessedAt`. Unique (CollaboratorId, SkillId) where not deleted.
2. **Manager notes** (greenfield, private)
   - `ManagerNote` — `CollaboratorId`, `AuthorId`, `Body` (markdown), `Pinned`, timestamps. Private review log; **never** visible to the subject.
3. **Direct-reports + access**
   - `GET /collaborators/my-team` — caller's direct reports (+ for HR/Admin, optionally all).
   - `ManagerAccess` authorization helper in the service layer: `caller is HR/Admin || target.ManagerId == caller.Id`. Reused by skills + notes endpoints.
4. **Controllers/services** following the `CareerEvent`/`Allocation` golden pattern, `[Authorize]`, FluentValidation, mappers in `Services/Mappers/`, enums stored as strings.

### Frontend

1. **"Management" tab/section** on `collaborator-detail-page.tsx`, gated by `isStaff || isManagerOf(target)`. Aggregates:
   - Current work (allocation, read-only embed)
   - Skills matrix (new, editable)
   - Performance (career events + feedback embeds + new manager notes, editable)
   - 1:1s (existing embed)
2. **Admin Team Overview** — new page `pages/team/team-overview-page.tsx`, HR/Admin only, sidebar + command-palette entries. Roster table: person · role · product/allocation · skill coverage · last 1:1 · recent performance signal · status. The live version of the vault roster dashboard.
3. A small **Tabs** primitive (Radix) added to the UI since none exists, or matched to the existing staff-gated section pattern — decided in the plan.
4. i18n keys in **all 3 locales** (pt-BR, en, es). Bump `WaaoFrontend/package.json` `1.26.1 → 1.27.0`.

## Non-goals (v1)

- No new 1:1/feedback mechanics — embed existing.
- No analytics/charts beyond simple coverage counts.
- No cross-tenant aggregation.
- No XP/gamification tie-in.

## Security / data safety

- Manager notes are private review data: enforce the "subject cannot read own notes" rule in the **service layer**, not just the UI.
- Migration: 3 new tables, all additive (no column changes to existing tables). Non-nullable columns get defaults; unique index on `CollaboratorSkill` filtered `where is_deleted = false`. Migration reviewed before apply; applied to dev DB first.

## Rollout

1. Backend: entities → migration (review → apply dev) → services/controllers → build `Waao.API`.
2. Frontend: services/types → Management tab → Team Overview page → nav + i18n ×3 → version bump.
3. Validate, then ship (Fly auto-deploy on push; **force-deploy frontend Worker** after push).
