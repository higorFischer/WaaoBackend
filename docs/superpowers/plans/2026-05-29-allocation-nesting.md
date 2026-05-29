# Allocation Canvas — Nested (Parent) Nodes Plan

> Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** Let a project box be nested inside another (node-in-node) by dragging it onto a parent box, and un-nested by dragging it out — React Flow subflows. Parents stay full projects (people + connections); arbitrary depth; admin-only.

**Architecture:** Add a self-referencing `Project.ParentProjectId`. Frontend renders nodes with React Flow `parentNode` + `extent:'parent'` (parents ordered before children, sized to contain children). Drag-to-nest is detected on `onNodeDragStop` via `getIntersectingNodes`; positions are persisted relative-to-parent (nested) or absolute (top-level). Cycles are prevented on both ends.

**Tech Stack:** .NET 9 / EF Core / FluentValidation / xUnit. React 19, `reactflow@11.11.4`, react-query.

**Reference:** existing canvas files `src/pages/allocations/allocation-board-page.tsx`, `project-node.tsx`; spec `docs/superpowers/specs/2026-05-29-allocation-canvas-design.md`.

---

## Task 1: Backend — ParentProjectId, service, endpoint, tests

**Files:**
- Modify `src/Waao.Domain.Models/Entities/Allocation/Project.cs`
- Modify `src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs`
- Migration (generated)
- Modify `src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs`
- Modify `src/Waao.Services.Abstractions/Services/IAllocationService.cs`
- Modify `src/Waao.Services/Validation/Allocation/AllocationValidators.cs`
- Modify `src/Waao.Services/Mappers/AllocationMapper.cs`
- Modify `src/Waao.Services/Services/Allocation/AllocationService.cs`
- Modify `src/Waao.API/Controllers/AllocationsController.cs`
- Modify `tests/Waao.Tests/Allocation/AllocationServiceTests.cs`

- [ ] **Step 1: Entity** — add to `Project`:

```csharp
	public Guid? ParentProjectId { get; set; }
	public virtual Project? Parent { get; set; }
	public virtual ICollection<Project> Children { get; set; } = [];
```

- [ ] **Step 2: EF config** — add to `ProjectConfiguration.Configure`:

```csharp
		builder.HasOne(x => x.Parent)
			.WithMany(p => p.Children)
			.HasForeignKey(x => x.ParentProjectId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.ParentProjectId);
```

- [ ] **Step 3: Migration**

```bash
dotnet build src/Waao.Infra.EF/Waao.Infra.EF.csproj
dotnet ef migrations add AddProjectParent -p src/Waao.Infra.EF -s src/Waao.API
```
Review: `Up()` adds nullable `parent_project_id` to `projects` + self-FK (Restrict) + index. No DROPs. Then:
```bash
dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API
```

- [ ] **Step 4: DTOs** — add `public Guid? ParentProjectId { get; init; }` to `ProjectWithAllocationsDto`, and add:

```csharp
public record SetParentDto
{
	public Guid? ParentProjectId { get; init; }
	public double X { get; init; }
	public double Y { get; init; }
}
```

- [ ] **Step 5: Interface** — add to `IAllocationService`:

```csharp
	Task SetProjectParentAsync(Guid projectId, SetParentDto dto, CancellationToken ct = default);
```

- [ ] **Step 6: Validator** — append to `AllocationValidators.cs`:

```csharp
public class SetParentValidator : AbstractValidator<SetParentDto>
{
	public SetParentValidator()
	{
		RuleFor(x => x.X).Must(double.IsFinite).WithMessage("X must be finite.");
		RuleFor(x => x.Y).Must(double.IsFinite).WithMessage("Y must be finite.");
	}
}
```

- [ ] **Step 7: Mapper** — add `ParentProjectId = p.ParentProjectId,` to the `Project` → `ProjectWithAllocationsDto` map.

- [ ] **Step 8: Service** — inject `IValidator<SetParentDto> SetParentValidator` into the `AllocationService` constructor (now 7 validators). Add:

```csharp
	public async Task SetProjectParentAsync(Guid projectId, SetParentDto dto, CancellationToken ct = default)
	{
		await SetParentValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		if (dto.ParentProjectId is { } parentId)
		{
			if (parentId == projectId)
				throw new InvalidOperationException("A project cannot be its own parent.");

			var parent = await Db.Projects.FirstOrDefaultAsync(p => p.Id == parentId, ct)
				?? throw new KeyNotFoundException($"Project {parentId} not found.");
			if (parent.IsArchived)
				throw new InvalidOperationException("Cannot nest under an archived project.");

			// Cycle guard: walk up from the proposed parent; if we reach projectId, it's a cycle.
			var chain = await Db.Projects.Select(p => new { p.Id, p.ParentProjectId }).ToListAsync(ct);
			var map = chain.ToDictionary(x => x.Id, x => x.ParentProjectId);
			var cursor = (Guid?)parentId;
			while (cursor is { } c)
			{
				if (c == projectId)
					throw new InvalidOperationException("Nesting would create a cycle.");
				cursor = map.TryGetValue(c, out var next) ? next : null;
			}
		}

		project.ParentProjectId = dto.ParentProjectId;
		project.PositionX = dto.X;
		project.PositionY = dto.Y;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
```

- [ ] **Step 9: Controller** — add (admin):

```csharp
	[HttpPut("projects/{id:guid}/parent")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> SetParent(Guid id, [FromBody] SetParentDto dto, CancellationToken ct)
	{
		await Service.SetProjectParentAsync(id, dto, ct);
		return NoContent();
	}
```

- [ ] **Step 10: Tests** — update the `Build()` helper to pass `new SetParentValidator()` (7 validators), and add:

```csharp
	[Fact]
	public async Task SetParent_NestsProject_BoardReflectsParent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var parent = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Platform" });
		var child = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Billing" });

		await svc.SetProjectParentAsync(child.Id, new SetParentDto { ParentProjectId = parent.Id, X = 20, Y = 60 });

		var box = (await svc.GetBoardAsync()).Projects.Single(p => p.Id == child.Id);
		box.ParentProjectId.Should().Be(parent.Id);
		box.PositionX.Should().Be(20);
	}

	[Fact]
	public async Task SetParent_Unnest_ClearsParent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var parent = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Platform" });
		var child = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Billing" });
		await svc.SetProjectParentAsync(child.Id, new SetParentDto { ParentProjectId = parent.Id, X = 10, Y = 10 });

		await svc.SetProjectParentAsync(child.Id, new SetParentDto { ParentProjectId = null, X = 300, Y = 120 });

		var box = (await svc.GetBoardAsync()).Projects.Single(p => p.Id == child.Id);
		box.ParentProjectId.Should().BeNull();
		box.PositionX.Should().Be(300);
	}

	[Fact]
	public async Task SetParent_SelfParent_Throws()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });

		var act = () => svc.SetProjectParentAsync(p.Id, new SetParentDto { ParentProjectId = p.Id, X = 0, Y = 0 });
		await act.Should().ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task SetParent_Cycle_Throws()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var a = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var b = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });
		await svc.SetProjectParentAsync(b.Id, new SetParentDto { ParentProjectId = a.Id, X = 0, Y = 0 }); // B under A

		// Now try to put A under B → cycle
		var act = () => svc.SetProjectParentAsync(a.Id, new SetParentDto { ParentProjectId = b.Id, X = 0, Y = 0 });
		await act.Should().ThrowAsync<InvalidOperationException>();
	}
```

- [ ] **Step 11: Build + test**

```bash
dotnet build src/Waao.API/Waao.API.csproj
dotnet test tests/Waao.Tests/Waao.Tests.csproj --filter "FullyQualifiedName~AllocationServiceTests"
```
Expected: 15 passed (11 prior + 4 new).

- [ ] **Step 12: Commit** (stage explicit files; NEVER combine `git commit` with a `.claude-flow` path; no AI words in message):

```bash
git add src/ tests/ && git commit -m "feat(allocation): nested projects (parent node) support"
```
(`git add src/ tests/` is safe — `.claude-flow` is at repo root, not under src/tests.)

---

## Task 2: Frontend — nesting types/service + drag-to-nest canvas

**Files:**
- Modify `src/types/allocations.types.ts`
- Modify `src/services/allocations.service.ts`
- Modify `src/pages/allocations/allocation-board-page.tsx`
- Modify `src/pages/allocations/project-node.tsx`

- [ ] **Step 1: Types** — add to `ProjectWithAllocations`: `parentProjectId?: string | null;`

- [ ] **Step 2: Service** — add to `allocationsService`:

```typescript
	setParent: async (id: string, parentProjectId: string | null, x: number, y: number): Promise<void> => {
		await apiClient.put(`/allocations/projects/${id}/parent`, { parentProjectId, x, y });
	},
```

- [ ] **Step 3: `project-node.tsx`** — make the node container size to fit children. Add an optional `hasChildren` to `ProjectNodeData` and, when true, give the node a larger min size and a "container" look so child nodes nest visually. Change the `ProjectNodeData` interface to add `hasChildren?: boolean;` and the root `<div>` className to:

```tsx
		<div
			className={cn(
				'rounded-lg border bg-card shadow-sm',
				hasChildren ? 'min-w-[320px] min-h-[220px]' : 'w-64',
			)}
```
(destructure `hasChildren` from `data`, and import `cn` from `@/lib/utils` — add the import.) Keep header/chips at the top; nested child nodes are rendered by React Flow positioned within this container.

- [ ] **Step 4: `allocation-board-page.tsx`** — add nesting. Changes:

(a) Import `useReactFlow` and `Node` type from reactflow:
```tsx
import ReactFlow, {
	Background, Controls, MiniMap, Panel, ReactFlowProvider,
	useNodesState, useEdgesState, addEdge, useReactFlow,
	type Connection, type Edge, type Node, type NodeDragHandler, type EdgeChange,
} from 'reactflow';
```

(b) Inside `BoardInner`, get the instance: `const { getIntersectingNodes } = useReactFlow();`

(c) Add the setParent mutation:
```tsx
	const setParent = useMutation({ mutationFn: (a: { id: string; parentId: string | null; x: number; y: number }) => allocationsService.setParent(a.id, a.parentId, a.x, a.y), onSuccess: invalidate });
```

(d) Replace the node-building `useEffect` body to (1) order parents before children and (2) set `parentNode`/`extent`/`hasChildren`:
```tsx
	useEffect(() => {
		const byId = new Map(board.projects.map(p => [p.id, p]));
		const childIds = new Set(board.projects.filter(p => p.parentProjectId && byId.has(p.parentProjectId)).map(p => p.parentProjectId as string));
		const depth = (p: typeof board.projects[number]): number => {
			let d = 0; let cur = p.parentProjectId;
			while (cur && byId.has(cur)) { d++; cur = byId.get(cur)!.parentProjectId; }
			return d;
		};
		const ordered = [...board.projects].sort((a, b) => depth(a) - depth(b)); // parents (shallower) first
		setNodes(ordered.map(p => ({
			id: p.id,
			type: 'project',
			position: { x: p.positionX, y: p.positionY },
			parentNode: p.parentProjectId && byId.has(p.parentProjectId) ? p.parentProjectId : undefined,
			extent: p.parentProjectId && byId.has(p.parentProjectId) ? 'parent' as const : undefined,
			data: { project: p, isStaff, hasChildren: childIds.has(p.id), onEdit, onDropPerson, onRemove: (id: string) => remove.mutate(id), onSaveNote: (id: string, note: string) => saveNote.mutate({ id, note }) },
		})));
		setEdges(board.connections.map(c => ({ id: c.id, source: c.sourceProjectId, target: c.targetProjectId, label: c.label ?? undefined })));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [data, isStaff]);
```

(e) Replace `onNodeDragStop` with drag-to-nest logic. A node dropped over another (non-descendant) node nests under it (relative position); dropped over nothing while nested un-nests (absolute position); otherwise just persists position:
```tsx
	const descendantsOf = useCallback((rootId: string): Set<string> => {
		const kids = new Map<string, string[]>();
		board.projects.forEach(p => { if (p.parentProjectId) { const arr = kids.get(p.parentProjectId) ?? []; arr.push(p.id); kids.set(p.parentProjectId, arr); } });
		const out = new Set<string>(); const stack = [rootId];
		while (stack.length) { const cur = stack.pop()!; (kids.get(cur) ?? []).forEach(k => { if (!out.has(k)) { out.add(k); stack.push(k); } }); }
		return out;
	}, [board.projects]);

	const onNodeDragStop: NodeDragHandler = useCallback((_e, node) => {
		const banned = descendantsOf(node.id); banned.add(node.id);
		const overlaps = getIntersectingNodes(node).filter(n => !banned.has(n.id));
		// pick the deepest (smallest area) intersecting candidate as the parent
		const target = overlaps.sort((a, b) => ((a.width ?? 0) * (a.height ?? 0)) - ((b.width ?? 0) * (b.height ?? 0)))[0] as Node | undefined;
		const abs = node.positionAbsolute ?? node.position;
		const currentParent = node.parentNode ?? null;

		if (target) {
			const tAbs = target.positionAbsolute ?? target.position;
			const relX = abs.x - tAbs.x;
			const relY = abs.y - tAbs.y;
			if (target.id !== currentParent) setParent.mutate({ id: node.id, parentId: target.id, x: relX, y: relY });
			else savePosition.mutate({ id: node.id, x: relX, y: relY });
		} else if (currentParent) {
			setParent.mutate({ id: node.id, parentId: null, x: abs.x, y: abs.y });
		} else {
			savePosition.mutate({ id: node.id, x: node.position.x, y: node.position.y });
		}
	}, [getIntersectingNodes, descendantsOf, setParent, savePosition]);
```

- [ ] **Step 5: Verify** — `npx tsc --noEmit` (no `any`; the `as Node | undefined` and existing `as` casts are fine).

- [ ] **Step 6: Build** — `npm run build`.

- [ ] **Step 7: Commit** — `git add src/ && git commit -m "feat(allocation): drag-to-nest parent/child project nodes"`

---

## Task 3: Manual verification

- [ ] Backend `dotnet run`, frontend `npm run dev`. As Admin at `/allocations`:
  - Create two boxes. Drag one ONTO the other → it nests inside (renders within the parent's area); reload → still nested.
  - Drag the child OUT of the parent → un-nests; reload → top-level.
  - Drop a person into the parent box and into the child box → both allocate (parent is still a full project).
  - Try to drag a parent into its own child → no nest (cycle prevented; backend would 400 too).
- [ ] Non-admin: nodes not draggable → cannot nest; can still drop people in.

---

## Deploy (on request)
Merge both branches to `main`, push (backend GH Action → Fly, migration auto-applies on startup, 1 machine). Frontend: after push, `npm run deploy` (Cloudflare).

## Self-review notes
- Coverage: ParentProjectId entity/migration/DTO/mapper (T1), set-parent service with self+cycle guards + tests (T1), admin endpoint (T1), drag-to-nest with `getIntersectingNodes` + descendant exclusion + relative/absolute position math (T2), parents ordered before children + sized container (T2). 
- Type consistency: `setParent(id, parentProjectId, x, y)` ↔ `SetParentDto {ParentProjectId, X, Y}`; `parentProjectId` field name backend↔frontend; `parentNode`/`extent` are reactflow built-ins.
- Risk flags: child position is relative-to-parent (persisted relative); `positionAbsolute` used for the math; cycle prevented on FE (descendant filter) AND BE (chain walk).
