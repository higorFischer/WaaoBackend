# Knowledge Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox steps.

**Goal:** An Obsidian-style force-directed graph in the Knowledge tab — docs as nodes, `[[wikilinks]]` as edges.

**Spec:** `docs/superpowers/specs/2026-05-22-knowledge-graph-design.md` — read first.

**Golden examples:** Backend — `src/Waao.Services/Documentation/DocumentationService.cs` (has the git cache, file enumeration, frontmatter regex), `src/Waao.API/Controllers/DocumentationController.cs`. Frontend — `src/pages/docs/DocsPage.tsx`, `src/pages/docs/components/DocTree.tsx`, `src/services/documentation.service.ts`, `src/types/documentation.types.ts`.

**Conventions:** Backend TABS, file-scoped namespaces, primary-ctor DI; `record` DTOs. Frontend no `any`, `t()` × 3 locales. Commit `git -c user.email="higor@waao.com.br"`, conventional prefix, no Claude/AI/Co-Authored-By. Push to `main`.

---

## Task 1: Backend — graph DTOs + service

**Files:**
- Add to `src/Waao.Services.Abstractions/Dtos/Documentation/DocumentationDtos.cs` — `DocGraphNodeDto`, `DocGraphEdgeDto`, `DocGraphDto` (records).
- Modify `src/Waao.Services.Abstractions/Services/IDocumentationService.cs` — add `GetGraphAsync`.
- Modify `src/Waao.Services/Documentation/DocumentationService.cs` — implement `GetGraphAsync` per spec: enumerate `.md`, build path + basename indexes, scan `[[wikilink]]` tokens (strip `|alias` and `#anchor`), resolve, build de-duped edges, compute `LinkCount`.

- [ ] Write a failing test `DocumentationGraphTests` — point `DocumentationOptions.LocalPath` at a temp fixture dir with a few `.md` files containing known `[[links]]` (incl. an unresolved one + a `[[Note|alias]]`); assert node count + resolved edge count + unresolved dropped.
- [ ] Run, confirm fails
- [ ] Implement `GetGraphAsync`
- [ ] Test passes; `dotnet build src/Waao.API/Waao.API.csproj` clean
- [ ] Commit: `feat(docs): knowledge graph service — nodes + wikilink edges`

## Task 2: Backend — endpoint

**Files:** Modify `src/Waao.API/Controllers/DocumentationController.cs` — `GET "graph"` → `DocGraphDto`, `[ProducesResponseType]`.

- [ ] Implement; `dotnet build` clean; `dotnet test` green
- [ ] `git push origin main`
- [ ] Commit: `feat(docs): GET /documentation/graph endpoint`

## Task 3: Frontend — types, service, dependency

**Files:**
- `WaaoFrontend/package.json` — add `react-force-graph-2d`; `npm install`.
- `src/types/documentation.types.ts` — `DocGraphNode`, `DocGraphEdge`, `DocGraph`.
- `src/services/documentation.service.ts` — `getGraph(): Promise<DocGraph>`.

- [ ] Implement; `npm run build` clean
- [ ] Commit: `feat(docs): knowledge graph types + service`

## Task 4: Frontend — DocGraph component + Tree/Graph toggle

**Files:**
- Create `src/pages/docs/components/DocGraph.tsx` — `<ForceGraph2D>`; props `{ currentPath, onSelect }`; fetches the graph via TanStack Query; node color by folder, radius by `linkCount`, label via `nodeCanvasObject`; current-doc node highlighted; click → `onSelect(path)`; hover highlights neighbors.
- Modify `src/pages/docs/DocsPage.tsx` — a `Tree | Graph` segmented toggle in the left pane header; Graph mode renders `DocGraph` instead of `DocTree`; node selection drives `currentPath` exactly like the tree.
- i18n `docs.view.tree` / `docs.view.graph` × 3 locales.

- [ ] Implement; `npm run build` clean
- [ ] `git push origin main`
- [ ] Commit: `feat(docs): Obsidian-style knowledge graph view + tree/graph toggle`

---

## Self-review
- Spec coverage: DTOs/service→T1, endpoint→T2, frontend types/service/dep→T3, component/toggle→T4. ✓
- `DocGraphDto { Nodes, Edges }` shape identical backend (T1) ↔ frontend `DocGraph` type (T3) ↔ consumer (T4). ✓
- No placeholders. ✓
