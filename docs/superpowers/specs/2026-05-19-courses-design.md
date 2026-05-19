# WAAO — Courses (internal training catalog)

- **Date:** 2026-05-19
- **Status:** Approved (design)
- **Repos:** `WaaoBackend` (primary), `WaaoFrontend`
- **Module:** Learning / Courses
- **Sequence:** Feature **E**, after the B→C→D→A roadmap. F (Tests) is a separate spec that ships after E.

## Goal

Replace the Courses nav coming-soon stub with a real internal training catalog. Admin/HR authors courses (title, description, provider, link, duration, suggested XP, category). Collaborators browse the catalog and **self-mark** courses as completed. Each completion lands in an admin review queue; an admin **manually grants XP** for the completion. The grant's `XpTransaction.reason` includes the course category, which the existing pattern-matched `SkillRadar` naturally picks up — completed courses drive the "Suas competências" chart with real learning data instead of guesses.

## Non-goals

- Quizzes / assessments (that is **Feature F**, a separate spec).
- File uploads (just `MaterialUrl` linking to external/internal materials).
- Prerequisites, learning paths, ratings, comments, certificates.
- Auto-granting XP. XP stays admin-only (Feature B contract preserved).

## Locked decisions

| # | Decision |
|---|----------|
| Workflow | Collaborator **self-marks** a course as completed. Admin reviews the completion and **manually grants XP**. |
| `IsPublished` | New courses are drafts by default (`IsPublished=false`); only published courses are visible to non-admin collaborators. |
| CRUD policy | Course create/update/publish require Admin OR HR (reuse the existing `[Authorize(Policy = "HR")]` policy which is Admin ∪ HR). Delete is Admin only. |
| `XpSource` | Reuse the existing `XpSource.Admin` enum value (no new enum). The course context is carried in the `reason` string and the `sourceEntityId`/`sourceEntityType` fields of the `XpTransaction`. |
| Nav | The Courses sidebar nav item routes to `/courses` (replacing its coming-soon target for that single nav link). |
| Radar feed | Each XP grant for a completion sets `XpTransaction.reason = "Course completed: <Title> [Category: <Category>]"`. The existing `SkillRadarCard.deriveSkills` pattern-matches the category text against its 6 axes — no radar refactor needed. |

## Backend changes (`Waao.API` / `.Services` / `.Domain.Models` / `.Infra.EF`)

### Entities

`Course` (new):
- `Id` (Guid v7), `Title` (≤200), `Description` (≤2000), `Provider` (≤120, nullable),
  `MaterialUrl` (≤500, nullable), `DurationMinutes` (int, nullable, ≥0),
  `SuggestedXp` (int, nullable, 0..10000),
  `Category` (≤80, required),
  `IsPublished` (bool, default false),
  `CreatedById` (Guid → Collaborator.Id), plus standard `CreatedAt`/`UpdatedAt`/`IsDeleted`/`DeletedAt`.

`CourseCompletion` (new):
- `Id` (Guid v7), `CourseId` (FK), `CollaboratorId` (FK), `CompletedAt` (DateTime),
  `Notes` (≤500, nullable),
  `XpAwarded` (int, nullable — null until admin grants),
  `XpAwardedAt` (DateTime, nullable),
  `XpAwardedByAdminId` (Guid, nullable → Collaborator.Id),
  plus `CreatedAt`/`UpdatedAt`/`IsDeleted`/`DeletedAt`.
- Unique constraint on `(CourseId, CollaboratorId)` where `IsDeleted = false`.

### Migration

`AddCoursesAndCompletions` — additive, no destructive ops, no data backfill (greenfield tables). Indexes:
- `Courses.(IsPublished, IsDeleted)` — list filter.
- `Courses.Category` — filter-by-category.
- `CourseCompletions.(CollaboratorId, IsDeleted)` — "my completions".
- `CourseCompletions.CourseId` — joins.
- Partial: `CourseCompletions.(XpAwardedAt) WHERE XpAwardedAt IS NULL AND IsDeleted = false` — admin pending-review queue.

Applied to local Postgres `WaaoLocal` and drift-checked before commit.

### DTOs (`Waao.Services.Abstractions/Dtos`)

- `CourseDto { Id, Title, Description, Provider, MaterialUrl, DurationMinutes, SuggestedXp, Category, IsPublished, CreatedById, CreatedAt }`
- `CreateCourseDto { Title, Description, Provider?, MaterialUrl?, DurationMinutes?, SuggestedXp?, Category }`
- `UpdateCourseDto` — same shape as `CreateCourseDto`.
- `CourseListFilterDto { Category?, OnlyPublished? }` (admin can pass `OnlyPublished=false` to see drafts; collaborators ignore the flag and always see only published).
- `CourseCompletionDto { Id, CourseId, CourseTitle, CourseCategory, CollaboratorId, CollaboratorName, CompletedAt, Notes, XpAwarded, XpAwardedAt, XpAwardedByAdminId, SuggestedXp }` — for the admin queue and the collaborator's own list.
- `MarkCourseCompleteDto { Notes? }`.
- `GrantCourseXpDto { int Amount }` — admin grants. `Amount > 0` (no negative on course completions — corrections go through the existing generic admin grant endpoint).

### Validators

- `CreateCourseValidator` / `UpdateCourseValidator` — Title NotEmpty ≤200, Description NotEmpty ≤2000, Category NotEmpty ≤80, URL ≤500, DurationMinutes 0..100000 if present, SuggestedXp 0..10000 if present.
- `MarkCourseCompleteValidator` — Notes ≤500 if present.
- `GrantCourseXpValidator` — Amount > 0, ≤10000.

### Services

- `ICourseService` (`Waao.Services.Abstractions.Services`):
  - `ListAsync(CourseListFilterDto filter, bool isAdminOrHr, ct)` — returns `CourseDto[]`. Non-Admin/HR callers always get only `IsPublished=true && !IsDeleted`.
  - `GetByIdAsync(Guid id, bool isAdminOrHr, ct)` — 404 for non-admin if unpublished/deleted.
  - `CreateAsync(CreateCourseDto dto, Guid authorId, ct)` (Admin/HR) → validates, persists with `IsPublished=false`, `Id=Guid.CreateVersion7()`, returns `CourseDto`.
  - `UpdateAsync(Guid id, UpdateCourseDto dto, ct)` (Admin/HR) → validates, sets `UpdatedAt=DateTime.UtcNow`, returns `CourseDto`.
  - `DeleteAsync(Guid id, ct)` (Admin) → soft delete.
  - `PublishAsync(Guid id, bool isPublished, ct)` (Admin/HR).

- `ICourseCompletionService` (`Waao.Services.Abstractions.Services`):
  - `MarkCompleteAsync(Guid courseId, Guid collaboratorId, MarkCourseCompleteDto dto, ct)`:
    validate dto; load course (404 if missing/deleted/unpublished — collaborators can't complete drafts);
    `if exists active completion → return existing dto (idempotent)`;
    else create `CourseCompletion` with `CompletedAt=UtcNow`, persist, return dto.
    NO XP side-effect at this point.
  - `ListPendingForReviewAsync(ct)` (Admin) — returns completions where
    `XpAwardedAt IS NULL AND IsDeleted = false`, joined with Course + Collaborator (course title + category + suggested XP + collaborator name).
  - `GrantXpForCompletionAsync(Guid completionId, GrantCourseXpDto dto, Guid adminId, ct)` (Admin):
    validate; load completion (404 if missing/deleted);
    `if XpAwardedAt is not null → idempotent return current dto`;
    call `GamificationEngine.RecordAsync(completion.CollaboratorId, dto.Amount, XpSource.Admin,
    $"Course completed: {course.Title} [Category: {course.Category}]", completionId, "CourseCompletion", ct)`;
    set `XpAwarded`, `XpAwardedAt=UtcNow`, `XpAwardedByAdminId=adminId`, `UpdatedAt=UtcNow`;
    SaveChanges. Returns the updated `CourseCompletionDto`.
  - `ListMyCompletionsAsync(Guid collaboratorId, ct)` — collaborator's own list.

### Controllers

`CoursesController` `[Route("api/waao/courses")] [Authorize]`:
- `GET ""` — list. Single endpoint with `[Authorize]` (any authed user). The action inspects `User.IsInRole("Admin") || User.IsInRole("HR")` and passes that flag to `ICourseService.ListAsync`, which applies the published-only filter for non-admin callers. (Avoids two endpoints / role-conditional attributes.)
- `GET "{id:guid}"` — detail. Same role-flag pattern (admin/HR can fetch drafts; collaborators get 404 on drafts).
- `POST ""` `[Authorize(Policy = "HR")]` — create.
- `PUT "{id:guid}"` `[Authorize(Policy = "HR")]` — update.
- `DELETE "{id:guid}"` `[Authorize(Policy = "Admin")]` — soft delete.
- `POST "{id:guid}/publish"` `[Authorize(Policy = "HR")]` — body `{ isPublished: bool }`.
- `POST "{id:guid}/complete"` — collaborator self-mark.
- `GET "me/completions"` — own list.

Admin queue endpoints folded into the existing `AdminController` (`/api/waao/admin`):
- `GET "course-completions/pending"` `[Authorize(Policy = "Admin")]` → `CourseCompletionDto[]`.
- `POST "course-completions/{id:guid}/grant-xp"` `[Authorize(Policy = "Admin")]` body `GrantCourseXpDto` → `CourseCompletionDto`.

### DI

`builder.Services.AddScoped<ICourseService, CourseService>();`
`builder.Services.AddScoped<ICourseCompletionService, CourseCompletionService>();`

(Sibling to existing service registrations in `Program.cs`. No other Program.cs change.)

## Frontend changes (`WaaoFrontend`, own UI lib)

- New route `/courses` (authed, inside `RequireAuth`/`AuthedShell`). Update the Courses sidebar nav item to point at `/courses` instead of the coming-soon target.
- New pages:
  - `CoursesPage` — list with category filter + search; cards show title / provider / duration / category badge / "Done" marker if already completed.
  - `CourseDetailPage` — full info + a single "Mark complete" action (idempotent; once completed shows "Completed on `<date>`" and either "XP pending" or "+`<N>` XP awarded"). Uses RHF only if Notes is collected; otherwise a bare button is fine.
  - `MyCoursesPage` (or a section in the existing `CollaboratorDetailPage`) — own completion history.
  - `AdminCoursesPage` (HR/Admin only, in the admin area) — CRUD: list with drafts visible, "New course" form, edit/publish/delete actions.
  - `AdminCourseReviewPage` (Admin only) — pending-review queue. Each row: Course (title, category badge), Collaborator name, completed date, suggested XP. Inline "Grant XP" form with `amount` prefilled to `course.suggestedXp ?? 0`; submit calls the grant-xp endpoint.
- Services: `courses.service.ts` (list/get/create/update/delete/publish/complete/me-completions); admin-side completion-review endpoints fold into `admin.service.ts` (grantCourseXp, listPendingCourseCompletions).
- Types in `src/types/waao.types.ts`: `Course`, `CourseCompletion`, `CreateCourseDto`, etc. (camelCase wire mirror).
- i18n: new `courses.*` namespace in all 3 locales (pt-BR canonical), covering list filters, detail labels, mark-complete CTA + states, admin CRUD form labels, admin review-queue copy.
- The existing SkillRadar/SkillRadarCard does NOT change — the category-tagged XP transactions feed it automatically through the existing pattern-matcher.

## Error contract

| Case | HTTP | Body |
|------|------|------|
| Create/update with invalid fields | 400 | FluentValidation `errors.<field>` shape |
| Get/Delete/Publish unknown course | 404 | `KeyNotFoundException` → existing middleware |
| Complete unpublished/missing course | 404 | as above |
| Complete already-completed | 200 | idempotent — returns existing `CourseCompletionDto` |
| Grant-XP unknown completion | 404 | as above |
| Grant-XP already-awarded | 200 | idempotent — returns existing dto |
| Non-Admin/HR attempting CRUD | 403 | policy enforcement |
| Non-Admin attempting grant-xp | 403 | policy enforcement |

## Testing

- **Backend unit tests** (`Waao.Tests`, EF InMemory):
  - Course CRUD: create requires Admin/HR; non-admin → 403; soft-deleted not listed; unpublished hidden from non-admin and 404 on GetById; admin/HR can list/list-by-id including drafts.
  - Validators: title/description/category required; URL/duration/XP within bounds.
  - `MarkComplete`: creates row, idempotent, rejects unpublished/missing course.
  - `ListPendingForReview`: lists only `XpAwardedAt IS NULL` completions; deleted excluded.
  - `GrantXpForCompletion`: writes `XpTransaction` via `GamificationEngine.RecordAsync` with `XpSource.Admin`, `reason = "Course completed: <title> [Category: <category>]"`, `sourceEntityId = completionId`, `sourceEntityType = "CourseCompletion"`; sets `XpAwarded`/`XpAwardedAt`/`XpAwardedByAdminId`; idempotent on second call.
  - **SkillRadar feed**: a completion with category `"Backend services"` yields an XpTransaction whose `reason` contains the substring `Backend`, satisfying the radar's existing matcher.
- **Migration**: applied to local `WaaoLocal`; drift-free; partial index verified via `\d course_completions`.
- **Frontend**: manual smoke (build clean; routes render; mark-complete flow; admin review + grant; the radar visibly responds to a sample completion).

## Rollout / safety

- New tables only. No destructive ops, no data backfill. Applied under the existing startup advisory lock.
- Deploy: backend `fly deploy --remote-only` (no CI/CD); frontend `git push origin main` (Cloudflare auto-deploy).
- After deploy, optionally seed 2–3 sample published courses through `DbInitializer.SeedDefaultCoursesAsync` (Admin convenience for the empty catalog — recommended, low scope).
- Backward compatibility: nothing existing changes. SkillRadar continues working with empty data; once admin grants XP for course completions, the chart starts reflecting real learning.

## Out of scope (for v1 — explicitly deferred)

- File uploads / certificate attachments (just MaterialUrl).
- Quizzes / assessments / pass-fail scoring → **Feature F (separate spec, queued after E)**.
- Prerequisites, learning paths/tracks, recommended-next.
- Ratings, comments, discussion threads.
- Auto-XP on completion (Feature B's admin-only XP rule is preserved; no auto-grant path is added).
- Bulk admin grant ("grant XP to everyone who completed X") — the admin grants one at a time from the queue.
