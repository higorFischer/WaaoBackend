# Allocation Board — "Quem está em quê"

**Date:** 2026-05-29
**Status:** Approved (design)

## Problem

Admins need to see, at a glance, **where each collaborator is currently working** — which
project/initiative and on which problem. They want to *configure* the set of projects (boxes)
and **drag collaborators into boxes**, with a short note describing the problem each person is on.

This is distinct from the existing **Kanban** module (Board → Column → Card), where the items
moved are *cards*. Here the items moved are **collaborators**, and a box is a *project/initiative*.

## Decisions (locked)

- **Boxes are a new `Project` entity** — configurable (title, color, description), not reused
  Kanban Boards or Courses.
- **Multiple allocations allowed** — a collaborator can sit in several boxes at once.
- **Granularity: project + free-text note** — each placement carries an optional note ("fixing
  billing bug"). No linked Kanban card in v1.
- **Permissions:** admins (`CollaboratorRoleKind.Admin`) configure boxes; **any** collaborator can
  drag people between boxes, edit notes, and remove allocations.
- **Layout: responsive grid of boxes** (like existing kanban columns), reorderable by admins —
  *not* free x/y canvas positioning.

## Out of scope (v1)

- Allocation **history/timeline UI** (basic history is implicitly retained via soft-delete +
  `AllocatedAt`, but no dedicated view).
- **Real-time** live updates via SignalR — v1 uses React Query refetch/invalidation. SignalR
  broadcast is a clean phase-2 add (WAAO already runs hubs on its single Fly machine).

## Data model

Two new entities, both `extends Entity` (Guid Id, soft-delete `IsDeleted`).

### `Project` (the box) — `Waao.Domain.Models/Entities/Allocation/Project.cs`
| Field | Type | Notes |
|-------|------|-------|
| `Title` | `string` | required, ≤ 120 |
| `Description` | `string?` | ≤ 1000 |
| `ColorHex` | `string` | default `#2A6B7E` (matches Board) |
| `Position` | `int` | ordering on the board |
| `IsArchived` | `bool` | archived boxes hidden from the board |
| `Allocations` | `ICollection<ProjectAllocation>` | nav |

### `ProjectAllocation` (person-in-box) — `.../Entities/Allocation/ProjectAllocation.cs`
| Field | Type | Notes |
|-------|------|-------|
| `ProjectId` | `Guid` | → Project |
| `CollaboratorId` | `Guid` | → Collaborator |
| `Note` | `string?` | the problem, ≤ 500 |
| `Position` | `int` | order within the box |
| `AllocatedAt` | `DateTime` | UtcNow on create |
| `AllocatedById` | `Guid?` | who placed them |

**Unique constraint:** `(ProjectId, CollaboratorId)` filtered `WHERE is_deleted = false` — a person
appears at most once per box, but may appear in many boxes.

EF config in `WaaoDbContext`: DbSets, snake_case, `!IsDeleted` query filters, FK indexes on
`project_id` and `collaborator_id`, the filtered unique index above. Migration
`AddAllocationBoard` (non-nullable columns get defaults; idempotent — WAAO is single-tenant).

## Backend

Follows `KanbanController` / kanban service patterns.

### DTOs (records, `init` setters)
- `ProjectDto` — id, title, description, colorHex, position, allocationCount
- `AllocationDto` — id, projectId, note, position, allocatedAt, `collaborator: CollaboratorChipDto`
- `CollaboratorChipDto` — id, fullName, photoUrl, roleName (reuse existing collaborator summary if present)
- `AllocationBoardDto` — `{ projects: ProjectWithAllocationsDto[], collaborators: CollaboratorChipDto[] }`
- `CreateProjectDto` / `UpdateProjectDto`, `ReorderProjectsDto { orderedIds }`
- `CreateAllocationDto { projectId, collaboratorId, note? }`
- `MoveAllocationDto { projectId, position }`, `UpdateNoteDto { note }`

### `IAllocationService` / `AllocationService`
- `GetBoardAsync()` — non-archived projects ordered by `Position`, each with allocations (ordered
  by `Position`) + collaborator chips; plus all **active** collaborators for the pool.
- `CreateProjectAsync` / `UpdateProjectAsync` / `ArchiveProjectAsync` / `ReorderProjectsAsync` — **admin**
- `AllocateAsync` — idempotent: if a non-deleted allocation for `(project, collaborator)` exists,
  return it (optionally update note); else create. Reject if project archived → 400.
- `MoveAllocationAsync` — change `ProjectId` and/or `Position`. If target box already has that
  collaborator, merge (no duplicate) and delete the moved row.
- `UpdateNoteAsync`, `RemoveAllocationAsync` (soft delete)
- `GetByCollaboratorAsync(collaboratorId)` — boxes a person is in (for profile page surface, optional wiring)

Validation via FluentValidation; mapping via `Services/Mappers/AllocationMapper`.

### `AllocationsController` — `[Route("api/allocations")]`, `[Authorize]`
| Verb | Route | Access |
|------|-------|--------|
| GET | `/board` | any |
| GET | `/by-collaborator/{id:guid}` | any |
| POST | `/projects` | admin |
| PUT | `/projects/{id:guid}` | admin |
| PUT | `/projects/reorder` | admin |
| DELETE | `/projects/{id:guid}` | admin (archive) |
| POST | `/` | any (allocate) |
| PUT | `/{id:guid}/move` | any |
| PUT | `/{id:guid}/note` | any |
| DELETE | `/{id:guid}` | any (remove) |

One-line expression-bodied actions, `IActionResult`, `[ProducesResponseType]`. Admin gate via the
project's existing role-check mechanism (`CollaboratorRoleKind.Admin` — match how `AdminController`
enforces it). Register service in DI.

## Frontend (`src/pages/allocations/`)

Reuses `board-view.tsx` `@dnd-kit` pattern, React Query, `@/components/ui`, react-i18next.

- **`allocation-board-page.tsx`** — `DndContext` with `PointerSensor` (distance 6). Boxes are
  `useDroppable`; collaborator chips are draggables. `DragOverlay` for the dragged chip.
- **Pool sidebar** — all active collaborators, searchable `Input`. Always draggable (multiple
  allowed). A badge marks anyone with **zero** allocations.
- **`allocation-chip.tsx`** — avatar + name + small italic note. Click opens inline note editor +
  remove (X). Drag pool→box = `allocate`; drag box→box = `move`.
- **Box** — header with title, color stripe, live count. Admin sees a gear → `project-edit-dialog.tsx`
  (create/edit/archive, color picker, reorder).
- **`allocations.service.ts`** — object service hitting `apiClient` (match `kanban.service.ts`).
- **`allocations.types.ts`** — no `any`.
- Optimistic updates on allocate/move/remove; invalidate `['allocation-board']` on settle.

### Data flow
1. Page → `GET /api/allocations/board`.
2. Drag pool→box → `POST /api/allocations { projectId, collaboratorId }` → optimistic add → invalidate.
3. Drag chip box→box → `PUT /api/allocations/{id}/move { projectId, position }`.
4. Edit note → `PUT /api/allocations/{id}/note { note }`.
5. Remove (X or drag to pool) → `DELETE /api/allocations/{id}`.
6. Admin box config → `POST/PUT/DELETE /api/allocations/projects…`.

### Navigation & i18n
- Register page in **Sidebar** and **CommandPalette** (WAAO's own components).
- New i18n namespace `allocations` in **pt-BR, en, es** (page title, box config labels, note
  placeholder, empty states, count labels).

## Error handling
- Allocate duplicate → idempotent no-op (return existing).
- Allocate/move into archived project → 400.
- Non-admin hits project-config endpoint → 403.
- Move/remove non-existent allocation → 404.

## Testing
- Backend service tests: allocate (new + idempotent duplicate), move (incl. merge-on-collision),
  archive hides from board, admin-gate enforcement, soft-delete on remove.
- Frontend: smoke test the board renders projects + pool; (full dnd e2e optional).

## Build/verify
- `dotnet build` on Waao.API passes; `dotnet ef database update` run locally.
- Frontend type-check/build passes.
- Deploy note (from memory): after `git push`, frontend **must** be force-deployed with
  `npm run deploy` (Cloudflare Worker auto-deploy does not work); waao-api stays at **1 Fly machine**.
