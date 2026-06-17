# Design Flow — Design Spec

- **Date:** 2026-06-17
- **Status:** Approved (design)
- **Scope:** A react-flow pipeline to track a product's visual-identity / design launch — freeform steps, many flows, files (PDF/icons/images) per step with a card-vs-full display toggle.

## Decisions (locked)
- **Freeform steps** — user creates/names/reorders/connects steps on a react-flow canvas; positions + edges persisted (like the Allocation board).
- **Many flows** — one `DesignFlow` per product/launch; a list page, open one → its board.
- **Access: all collaborators** — `[Authorize]` (any authenticated user) can view + contribute. No Admin gate.

## Reuse (read these before coding)
- react-flow board: `WaaoFrontend/src/pages/allocations/allocation-board-page.tsx` + `project-node.tsx` (custom nodes, positions, connections, `reactflow/dist/style.css`).
- R2 file upload: the existing **tenant-logo** upload path (find via grep `R2`/`logo`/`upload` in `WaaoBackend/src` — reuse the same R2 client/config; design assets are non-secret → public URLs, like avatars/tenant logos).
- Entity/migration patterns: the Team Management feature (`Skill`/`CollaboratorSkill`, `20260615201146_AddTeamManagement`).

## Backend (4 additive tables, `/api/waao/design-flows`)

### Entities
- **`DesignFlow`** (tenant-scoped): `Name`, `Description?`, `Status` (enum `DesignFlowStatus`: Active, Archived), soft-delete.
- **`DesignStep`**: `FlowId`, `Title`, `Description?`, `Status` (enum `DesignStepStatus`: NotStarted, InProgress, Done), `PositionX` (double), `PositionY` (double).
- **`DesignStepEdge`**: `FlowId`, `SourceStepId`, `TargetStepId`.
- **`DesignAsset`**: `StepId`, `FileName`, `ContentType`, `Kind` (enum `DesignAssetKind`: Pdf, Icon, Image, Other), `Url` (R2), `R2Key`, `SizeBytes`, `ShowFullByDefault` (bool), `UploadedById`.
- Enums stored as string (`HasConversion<string>()`).

### API contract (camelCase JSON; SkillLevel-style string enums)
- `GET /design-flows` → `DesignFlowDto[]` (`id, name, description?, status, stepCount, updatedAt`)
- `POST /design-flows` (`{name, description?}`) → `DesignFlowDto`
- `PUT /design-flows/{id}` (`{name, description?, status}`) → `DesignFlowDto`
- `DELETE /design-flows/{id}` → 204 (soft delete)
- `GET /design-flows/{id}/board` → `DesignBoardDto` (`flow: DesignFlowDto`, `steps: DesignStepDto[]`, `edges: DesignEdgeDto[]`)
- `POST /design-flows/{id}/steps` (`{title, description?, positionX, positionY}`) → `DesignStepDto`
- `PUT /steps/{id}` (`{title?, description?, status?, positionX?, positionY?}`) → `DesignStepDto`
- `DELETE /steps/{id}` → 204
- `POST /design-flows/{id}/edges` (`{sourceStepId, targetStepId}`) → `DesignEdgeDto`
- `DELETE /edges/{id}` → 204
- `POST /steps/{id}/assets` (multipart file upload) → `DesignAssetDto` (uploads to R2)
- `PUT /assets/{id}` (`{showFullByDefault}`) → `DesignAssetDto`
- `DELETE /assets/{id}` → 204

### DTO fields
- `DesignStepDto`: `id, flowId, title, description?, status, positionX, positionY, assets: DesignAssetDto[]`
- `DesignEdgeDto`: `id, flowId, sourceStepId, targetStepId`
- `DesignAssetDto`: `id, stepId, fileName, contentType, kind, url, sizeBytes, showFullByDefault, uploadedById, createdAt`

## Frontend
- **`/design`** list page — flow cards (name, status, stepCount) + "New flow" dialog. Sidebar nav + command... (no command palette in WAAO → sidebar only).
- **Flow board** `/design/{id}` — react-flow mirroring the Allocation board:
  - Custom `DesignStepNode`: title, status dot/color (NotStarted grey / InProgress amber / Done green), asset-count + small thumbnails. Drag → persist `positionX/Y`. Draw connections → `POST edges`. Add/rename/delete steps, change status.
  - Click a node → **step asset panel** (side panel/dialog): upload PDF/icon/image (multipart), list assets as cards (icon + name + download); per-asset **"show fully"** toggle → renders PDF inline via `<embed src={url}>` / image via `<img>`; toggling persists `showFullByDefault`.
- TanStack Query, `apiClient`, `@/` alias, NO `any`, i18n ×3 (namespace `design`). Bump `1.28.0 → 1.29.0`.

## Non-goals (v1)
- No real-time collaboration/locking. No versioning of assets. No PDF annotation. No per-user display prefs (the toggle is a shared default on the asset).

## Rollout
Backend: entities → reviewed migration (dev only) → R2 upload → services/controllers → build. Frontend: types/services → list page → board → asset panel → nav + i18n ×3 → version bump. Validate; deploy only on explicit go (manual: flyctl backend, npm run deploy frontend).
