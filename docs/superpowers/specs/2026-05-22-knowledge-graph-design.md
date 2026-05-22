# WAAO — Knowledge Graph (Obsidian-style graph view)

- **Date:** 2026-05-22
- **Status:** Approved (design)
- **Repos:** `WaaoBackend`, `WaaoFrontend`
- **Module:** Documentation / Knowledge

## Goal

Add an Obsidian-style graph view to the Knowledge ("Conhecimento") tab: every doc is a node, every `[[wikilink]]` an edge, in an interactive force-directed graph. Clicking a node opens that doc.

## Locked decisions

| # | Decision |
|---|----------|
| Scope | The **global** knowledge graph (whole vault). Per-doc "local graph" deferred. |
| Placement | A **Tree ⇄ Graph** toggle in the Knowledge tab's left sidebar — graph replaces the tree pane when active; the doc viewer pane stays. |
| Library | `react-force-graph-2d` (canvas, performant, Obsidian-like). One new frontend dependency. |
| Edge source | `[[wikilink]]` tokens in `.md` content. Unresolved links (target doc doesn't exist) are dropped. |

## Backend (`WaaoBackend`)

### `DocumentationService` — `GetGraphAsync`

`Task<DocGraphDto> GetGraphAsync(CancellationToken ct)`:
1. `EnsureClonedAsync` (same git-backed cache the tree/file endpoints use).
2. Enumerate every `.md` under the docs root (skip `HiddenFolders`). Each → a node: `Id` = relative path (`/`-normalized), `Label` = filename without `.md`, `Folder` = parent dir relative path.
3. Build a basename index (lowercased filename stem → path) and a path index — to resolve wikilinks, mirroring the frontend's existing `buildBasenameIndex` logic.
4. For each file, read content, match `\[\[([^\]]+)\]\]`. For each token: take the part before any `|` (alias) and before any `#` (heading anchor); if it contains `/`, resolve against the path index, else against the basename index. A resolved target → an edge `{ Source: thisDocPath, Target: resolvedPath }`. Drop unresolved + self-links. De-dupe edges.
5. Set each node's `LinkCount` = number of edges touching it (in or out).
6. Return `DocGraphDto`.

### DTOs (`Waao.Services.Abstractions/Dtos/Documentation/`)

- `DocGraphNodeDto { string Id, string Label, string Folder, int LinkCount }`
- `DocGraphEdgeDto { string Source, string Target }`
- `DocGraphDto { IReadOnlyList<DocGraphNodeDto> Nodes, IReadOnlyList<DocGraphEdgeDto> Edges }`

### Controller

`DocumentationController` — `GET "graph"` → `DocGraphDto`. `[Authorize]` like the other documentation endpoints.

### `IDocumentationService`

Add `GetGraphAsync` to the interface.

## Frontend (`WaaoFrontend`)

- `package.json` — add `react-force-graph-2d`.
- `src/types/documentation.types.ts` — add `DocGraphNode`, `DocGraphEdge`, `DocGraph`.
- `src/services/documentation.service.ts` — add `getGraph(): Promise<DocGraph>`.
- New `src/pages/docs/components/DocGraph.tsx`:
  - Renders `<ForceGraph2D>` with the nodes/edges.
  - Node color by folder (a small hashed palette); node radius scales mildly with `linkCount`.
  - Node label drawn next to the node (canvas `nodeCanvasObject`).
  - The currently-open doc's node is accent-highlighted.
  - Click a node → calls `onSelect(path)` → the parent opens that doc.
  - Hover a node → highlight its edges + neighbors, dim the rest.
  - Pan / zoom / drag (built into the library).
- `src/pages/docs/DocsPage.tsx` — a `Tree | Graph` segmented toggle above the left pane. Graph mode swaps `DocTree` for `DocGraph`; selecting a node sets `currentPath` exactly like the tree does. The right-hand `DocViewer` pane is unchanged.
- i18n — `docs.view.tree` / `docs.view.graph` keys × 3 locales.

## Error contract

| Case | HTTP |
|------|------|
| Docs repo not reachable (clone fails) | 500 — existing middleware (same as tree/file today) |
| Empty repo | 200 with empty `nodes`/`edges` |

## Testing

- Backend: a `GetGraphAsync` test over a small fixture doc set — N `.md` files with known `[[links]]`; assert node count, resolved edge count, that an unresolved `[[Ghost]]` produces no edge, that `[[Note|alias]]` and `[[Note#heading]]` resolve to `Note`.
- Frontend: manual smoke — toggle to Graph, nodes render, click opens the doc, current doc highlighted.

## Rollout

- New endpoint + new frontend view; no migration, no schema change.
- Depends on the Knowledge tab actually having docs (the `git` install + `WaaoDocs` PAT must be in place — separate, already-tracked work). With no docs, the graph is simply empty.
- Backend push → Fly CI/CD; frontend push → Cloudflare.

## Out of scope

- Per-doc local graph (open doc + immediate neighbors)
- Tag nodes, folder-group nodes, orphan filtering, search-in-graph
- Graph layout persistence
