# Allocation Board → React Flow Canvas (redesign)

**Date:** 2026-05-29
**Status:** Approved (design)
**Supersedes:** the responsive-grid layout in `2026-05-29-allocation-board-design.md` (frontend layout only — the allocation backend/data model is kept and extended).

## Problem

The shipped Allocation Board used a responsive grid of boxes. The user wants a **React Flow
canvas** instead: project boxes freely positioned, connectable with edges to show relationships
("charts and graphs"), pan/zoom, persisted layout. Sibling products (MentalHealth, UPA, Oncologia)
already use `reactflow@^11.11.4` — reference: `MentalHealthFrontend/.../workflow/WorkflowBuilder.tsx`.

## Decisions (locked)

- **Model A — boxes are nodes, people live inside.** Each `Project` is a React Flow node; you drag
  collaborators INTO a node (keeps the existing `ProjectAllocation` model). Edges connect
  project↔project (relationships/dependencies), with an optional label.
- **Persist everything** — node `(x, y)` positions on `Project`, and connections as a new table.
- **Permissions:** moving/connecting nodes (= board configuration) is **admin** (`Admin`/`HR`);
  dropping people into a box and editing notes stays open to **any** authenticated user.
- **DnD:** the page drops `@dnd-kit` (React Flow owns canvas drag). Pool→node allocate and
  chip→node move use **native HTML5 drag-and-drop**; draggable inner elements get React Flow's
  `nodrag` class so the node isn't dragged instead.

## Data model changes

### `Project` — add two fields
- `PositionX` (`double`, default 0), `PositionY` (`double`, default 0) — canvas coordinates.
- Existing `Position` (int) is now unused for layout but kept (no destructive change).

### New `ProjectConnection` — `.../Entities/Allocation/ProjectConnection.cs` (`extends Entity`)
| Field | Type | Notes |
|-------|------|-------|
| `SourceProjectId` | `Guid` | → Project |
| `TargetProjectId` | `Guid` | → Project |
| `Label` | `string?` | optional edge label, ≤ 120 |

Unique per `(SourceProjectId, TargetProjectId)` filtered `is_deleted = false`. Soft-delete.
Cascade from Project. Migration `AddAllocationCanvas` (add 2 columns + new table; non-destructive,
new columns default 0).

## Backend

DTO/service/controller additions (on top of the shipped allocation feature):
- `ProjectWithAllocationsDto` gains `PositionX`, `PositionY`.
- `AllocationBoardDto` gains `Connections: IReadOnlyList<ProjectConnectionDto>`.
- New: `ProjectConnectionDto { Id, SourceProjectId, TargetProjectId, Label }`,
  `CreateConnectionDto { SourceProjectId, TargetProjectId, Label? }`,
  `UpdatePositionDto { X, Y }`.
- `IAllocationService` / `AllocationService`:
  - `GetBoardAsync` also returns non-deleted connections; projects carry their X/Y.
  - `UpdateProjectPositionAsync(projectId, UpdatePositionDto)` — **admin**.
  - `CreateConnectionAsync(CreateConnectionDto)` — **admin**; idempotent per (source,target);
    reject self-connection (source == target) → 400; reject if either project missing/archived.
  - `RemoveConnectionAsync(connectionId)` — **admin**; soft delete.
  - `CreateProjectAsync` seeds a staggered initial position so new boxes don't stack at (0,0).
- `AllocationsController` new endpoints:
  - `PUT /projects/{id:guid}/position` (admin)
  - `POST /connections` (admin)
  - `DELETE /connections/{id:guid}` (admin)
- Validators for `CreateConnectionDto` (ids non-empty, label ≤120) and `UpdatePositionDto`
  (X/Y finite).

## Frontend

Add `reactflow@^11.11.4` + `import 'reactflow/dist/style.css'`. Rebuild
`src/pages/allocations/allocation-board-page.tsx`:

- `ReactFlowProvider` wrapping `<ReactFlow>` with `Background`, `Controls`, `MiniMap`, and a `Panel`
  for the "New project" button (admin). `nodeTypes = { project: ProjectNode }`.
- Nodes derived from `board.projects` (position `{ x: positionX, y: positionY }`). Edges from
  `board.connections` (`{ id, source: sourceProjectId, target: targetProjectId, label }`).
- `nodesDraggable` / `nodesConnectable` / `elementsSelectable` = `isStaff` (Admin/HR). Non-staff get
  a read-only canvas they can pan/zoom and still drop people into.
- **`ProjectNode`** (custom node, new file `project-node.tsx`): renders the box (color stripe,
  title, count, admin gear → `ProjectEditDialog`), the allocation chips inside, source+target
  `Handle`s. The chips list and the node body are a native **drop zone**: `onDragOver`/`onDrop`
  read `dataTransfer` — `pool:<collaboratorId>` → allocate; `alloc:<allocationId>` → move. Inner
  interactive elements carry the `nodrag` class.
- Pool sidebar (searchable, all active collaborators, unallocated badge) unchanged in purpose;
  chips become native-draggable (`draggable`, `onDragStart` sets `dataTransfer`).
- Handlers: `onNodeDragStop` → `updatePosition` (admin); `onConnect` → `createConnection` then
  invalidate; `onEdgesDelete` → `removeConnection`. All mutate via React Query, invalidate
  `['allocation-board']`.

## Out of scope (v1)
- Edge styling/types beyond a label. Node resize. Auto-layout. Real-time (still React Query
  invalidate). Per-user canvas views (layout is global/shared).

## Build/verify/deploy
- Backend builds; migration auto-applies on prod startup (`MigrateAsync`).
- Frontend `tsc` + `npm run build`; after push, **`npm run deploy`** (Cloudflare). waao-api stays at
  **1 machine**.
