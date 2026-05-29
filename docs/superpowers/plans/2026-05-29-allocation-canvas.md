# Allocation Canvas (React Flow) Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Replace the grid layout of the Allocation Board with a React Flow canvas — freely-positioned project nodes (positions persisted), project↔project connection edges (persisted), people dropped into nodes via native HTML5 DnD.

**Architecture:** Extend the shipped allocation backend with `Project.PositionX/Y` + a new `ProjectConnection` entity and endpoints. Rebuild the frontend page on `reactflow@^11.11.4` with a custom `ProjectNode`. Admins move/connect nodes; anyone drops people in.

**Tech Stack:** .NET 9 / EF Core / FluentValidation / xUnit. React 19, `reactflow@^11.11.4`, react-query, react-i18next.

**Reference:** `Repositories/MentalHealth/MentalHealthFrontend/src/components/exams/workflow/WorkflowBuilder.tsx` (React Flow v11 pattern). Shipped allocation spec: `docs/superpowers/specs/2026-05-29-allocation-board-design.md`. This redesign: `.../specs/2026-05-29-allocation-canvas-design.md`.

---

## Task 1: Backend — positions, ProjectConnection entity, migration

**Files:**
- Modify: `src/Waao.Domain.Models/Entities/Allocation/Project.cs`
- Create: `src/Waao.Domain.Models/Entities/Allocation/ProjectConnection.cs`
- Modify: `src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs`
- Modify: `src/Waao.Infra.EF/WaaoDbContext.cs`
- Migration (generated)

- [ ] **Step 1: Add positions to `Project`** — add inside the `Project` class:

```csharp
	public double PositionX { get; set; }
	public double PositionY { get; set; }
	public virtual ICollection<ProjectConnection> OutgoingConnections { get; set; } = [];
```

- [ ] **Step 2: Create `ProjectConnection.cs`**

```csharp
namespace Waao.Domain.Models.Entities.Allocation;

public class ProjectConnection : Entity
{
	public Guid SourceProjectId { get; set; }
	public virtual Project SourceProject { get; set; } = null!;

	public Guid TargetProjectId { get; set; }
	public virtual Project TargetProject { get; set; } = null!;

	public string? Label { get; set; }
}
```

- [ ] **Step 3: EF config** — append to `AllocationConfiguration.cs`:

```csharp
public class ProjectConnectionConfiguration : IEntityTypeConfiguration<ProjectConnection>
{
	public void Configure(EntityTypeBuilder<ProjectConnection> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Label).HasMaxLength(120);

		builder.HasOne(x => x.SourceProject)
			.WithMany(p => p.OutgoingConnections)
			.HasForeignKey(x => x.SourceProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.TargetProject)
			.WithMany()
			.HasForeignKey(x => x.TargetProjectId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.SourceProjectId, x.TargetProjectId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
```

> Note: one FK is Cascade, the other Restrict, to avoid SQL Server-style multiple-cascade-path errors; PostgreSQL tolerates it but Restrict on target is safest. Keep `PositionX/PositionY` unconfigured (defaults to `double` columns).

- [ ] **Step 4: DbSet** — add after `ProjectAllocations` in `WaaoDbContext.cs`:

```csharp
	public DbSet<Waao.Domain.Models.Entities.Allocation.ProjectConnection> ProjectConnections => Set<Waao.Domain.Models.Entities.Allocation.ProjectConnection>();
```

- [ ] **Step 5: Build + migration**

```bash
dotnet build src/Waao.Infra.EF/Waao.Infra.EF.csproj
dotnet ef migrations add AddAllocationCanvas -p src/Waao.Infra.EF -s src/Waao.API
```
Review: `Up()` adds `position_x`/`position_y` (double, default 0) to `projects`, creates `project_connections` with the filtered unique index, no DROPs. Then:
```bash
dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(allocation): canvas positions and ProjectConnection entity"
```

---

## Task 2: Backend — DTOs, validators, service, mapper

**Files:**
- Modify: `src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs`
- Modify: `src/Waao.Services.Abstractions/Services/IAllocationService.cs`
- Modify: `src/Waao.Services/Validation/Allocation/AllocationValidators.cs`
- Modify: `src/Waao.Services/Mappers/AllocationMapper.cs`
- Modify: `src/Waao.Services/Services/Allocation/AllocationService.cs`

- [ ] **Step 1: DTOs** — add positions to `ProjectWithAllocationsDto` (add `public double PositionX { get; init; }` and `PositionY`), add `Connections` to `AllocationBoardDto` (`public IReadOnlyList<ProjectConnectionDto> Connections { get; init; } = [];`), and append:

```csharp
public record ProjectConnectionDto
{
	public Guid Id { get; init; }
	public Guid SourceProjectId { get; init; }
	public Guid TargetProjectId { get; init; }
	public string? Label { get; init; }
}

public record CreateConnectionDto
{
	public Guid SourceProjectId { get; init; }
	public Guid TargetProjectId { get; init; }
	public string? Label { get; init; }
}

public record UpdatePositionDto
{
	public double X { get; init; }
	public double Y { get; init; }
}
```

- [ ] **Step 2: Interface** — add to `IAllocationService`:

```csharp
	Task UpdateProjectPositionAsync(Guid projectId, UpdatePositionDto dto, CancellationToken ct = default);
	Task<ProjectConnectionDto> CreateConnectionAsync(CreateConnectionDto dto, CancellationToken ct = default);
	Task RemoveConnectionAsync(Guid connectionId, CancellationToken ct = default);
```

- [ ] **Step 3: Validators** — append to `AllocationValidators.cs`:

```csharp
public class CreateConnectionValidator : AbstractValidator<CreateConnectionDto>
{
	public CreateConnectionValidator()
	{
		RuleFor(x => x.SourceProjectId).NotEmpty();
		RuleFor(x => x.TargetProjectId).NotEmpty();
		RuleFor(x => x.TargetProjectId).NotEqual(x => x.SourceProjectId).WithMessage("A project cannot connect to itself.");
		RuleFor(x => x.Label).MaximumLength(120).When(x => x.Label is not null);
	}
}

public class UpdatePositionValidator : AbstractValidator<UpdatePositionDto>
{
	public UpdatePositionValidator()
	{
		RuleFor(x => x.X).Must(double.IsFinite).WithMessage("X must be finite.");
		RuleFor(x => x.Y).Must(double.IsFinite).WithMessage("Y must be finite.");
	}
}
```

- [ ] **Step 4: Mapper** — add positions to the `Project` → `ProjectWithAllocationsDto` map (`PositionX = p.PositionX, PositionY = p.PositionY,`) and add:

```csharp
	public static ProjectConnectionDto ToDto(ProjectConnection c) => new()
	{
		Id = c.Id,
		SourceProjectId = c.SourceProjectId,
		TargetProjectId = c.TargetProjectId,
		Label = c.Label,
	};
```

- [ ] **Step 5: Service** — inject the two new validators into the `AllocationService` primary constructor (`IValidator<CreateConnectionDto> CreateConnectionValidator, IValidator<UpdatePositionDto> UpdatePositionValidator`). Update `GetBoardAsync` to also load connections and return them:

```csharp
		var connections = await Db.ProjectConnections
			.AsNoTracking()
			.Where(c => !c.SourceProject.IsArchived && !c.TargetProject.IsArchived)
			.ToListAsync(ct);
```
and include `Connections = connections.Select(AllocationMapper.ToDto).ToList()` in the returned `AllocationBoardDto`. In `CreateProjectAsync`, seed a staggered position so boxes don't stack: `PositionX = (maxPos + 1) % 4 * 280, PositionY = (maxPos + 1) / 4 * 200,` (use the existing `maxPos`). Add the three methods:

```csharp
	public async Task UpdateProjectPositionAsync(Guid projectId, UpdatePositionDto dto, CancellationToken ct = default)
	{
		await UpdatePositionValidator.ValidateAndThrowAsync(dto, ct);
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");
		project.PositionX = dto.X;
		project.PositionY = dto.Y;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task<ProjectConnectionDto> CreateConnectionAsync(CreateConnectionDto dto, CancellationToken ct = default)
	{
		await CreateConnectionValidator.ValidateAndThrowAsync(dto, ct);

		var projects = await Db.Projects.Where(p => p.Id == dto.SourceProjectId || p.Id == dto.TargetProjectId).ToListAsync(ct);
		var source = projects.FirstOrDefault(p => p.Id == dto.SourceProjectId) ?? throw new KeyNotFoundException($"Project {dto.SourceProjectId} not found.");
		var target = projects.FirstOrDefault(p => p.Id == dto.TargetProjectId) ?? throw new KeyNotFoundException($"Project {dto.TargetProjectId} not found.");
		if (source.IsArchived || target.IsArchived)
			throw new InvalidOperationException("Cannot connect an archived project.");

		var existing = await Db.ProjectConnections
			.FirstOrDefaultAsync(c => c.SourceProjectId == dto.SourceProjectId && c.TargetProjectId == dto.TargetProjectId, ct);
		if (existing != null)
		{
			if (dto.Label != null) { existing.Label = dto.Label; existing.UpdatedAt = DateTime.UtcNow; await Db.SaveChangesAsync(ct); }
			return AllocationMapper.ToDto(existing);
		}

		var conn = new ProjectConnection
		{
			Id = Guid.CreateVersion7(),
			SourceProjectId = dto.SourceProjectId,
			TargetProjectId = dto.TargetProjectId,
			Label = dto.Label,
		};
		Db.ProjectConnections.Add(conn);
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(conn);
	}

	public async Task RemoveConnectionAsync(Guid connectionId, CancellationToken ct = default)
	{
		var conn = await Db.ProjectConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct)
			?? throw new KeyNotFoundException($"Connection {connectionId} not found.");
		conn.IsDeleted = true;
		conn.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
```

- [ ] **Step 6: Build**

```bash
dotnet build src/Waao.Services/Waao.Services.csproj
```

- [ ] **Step 7: Commit** — `git commit -am "feat(allocation): connection + position service, DTOs, validators"`

---

## Task 3: Backend — controller endpoints + tests

**Files:**
- Modify: `src/Waao.API/Controllers/AllocationsController.cs`
- Modify: `tests/Waao.Tests/Allocation/AllocationServiceTests.cs`

- [ ] **Step 1: Controller** — add (admin-gated for position + connections):

```csharp
	[HttpPut("projects/{id:guid}/position")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] UpdatePositionDto dto, CancellationToken ct)
	{
		await Service.UpdateProjectPositionAsync(id, dto, ct);
		return NoContent();
	}

	[HttpPost("connections")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(ProjectConnectionDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateConnection([FromBody] CreateConnectionDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateConnectionAsync(dto, ct));

	[HttpDelete("connections/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> RemoveConnection(Guid id, CancellationToken ct)
	{
		await Service.RemoveConnectionAsync(id, ct);
		return NoContent();
	}
```

- [ ] **Step 2: Update test `Build()` helper** to pass the two new validators:

```csharp
	private static AllocationService Build(Waao.Infra.EF.WaaoDbContext db) =>
		new(db, new CreateProjectValidator(), new UpdateProjectValidator(),
			new CreateAllocationValidator(), new UpdateNoteValidator(),
			new CreateConnectionValidator(), new UpdatePositionValidator());
```
(add `using Waao.Services.Abstractions.Dtos.Allocation;` if needed — already present.)

- [ ] **Step 3: Add tests** for connections + position:

```csharp
	[Fact]
	public async Task CreateConnection_ThenBoard_ReturnsEdge()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });

		await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id, Label = "depends" });
		var board = await svc.GetBoardAsync();

		board.Connections.Should().ContainSingle(c => c.SourceProjectId == p1.Id && c.TargetProjectId == p2.Id && c.Label == "depends");
	}

	[Fact]
	public async Task CreateConnection_Duplicate_IsIdempotent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });

		var first = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id });
		var second = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id, Label = "x" });

		second.Id.Should().Be(first.Id);
		(await svc.GetBoardAsync()).Connections.Should().ContainSingle();
	}

	[Fact]
	public async Task RemoveConnection_DropsEdgeFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "B" });
		var c = await svc.CreateConnectionAsync(new CreateConnectionDto { SourceProjectId = p1.Id, TargetProjectId = p2.Id });

		await svc.RemoveConnectionAsync(c.Id);

		(await svc.GetBoardAsync()).Connections.Should().BeEmpty();
	}

	[Fact]
	public async Task UpdatePosition_PersistsOnBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var p = await svc.CreateProjectAsync(new CreateProjectDto { Title = "A" });

		await svc.UpdateProjectPositionAsync(p.Id, new UpdatePositionDto { X = 123, Y = 456 });

		var box = (await svc.GetBoardAsync()).Projects.Single(x => x.Id == p.Id);
		box.PositionX.Should().Be(123);
		box.PositionY.Should().Be(456);
	}
```

- [ ] **Step 4: Build API + run full tests**

```bash
dotnet build src/Waao.API/Waao.API.csproj
dotnet test tests/Waao.Tests/Waao.Tests.csproj --filter "FullyQualifiedName~AllocationServiceTests"
```
Expected: all allocation tests pass (7 prior + 4 new = 11).

- [ ] **Step 5: Commit** — `git commit -am "feat(allocation): connection/position endpoints + tests"`

---

## Task 4: Frontend — reactflow dep, types, service

**Files:**
- Modify: `package.json` (add `reactflow`)
- Modify: `src/types/allocations.types.ts`
- Modify: `src/services/allocations.service.ts`

- [ ] **Step 1: Add dependency**

```bash
npm install reactflow@^11.11.4
```

- [ ] **Step 2: Types** — add `positionX: number; positionY: number;` to `ProjectWithAllocations`; add `connections: ProjectConnection[]` to `AllocationBoard`; add:

```typescript
export interface ProjectConnection {
	id: string;
	sourceProjectId: string;
	targetProjectId: string;
	label?: string | null;
}
```

- [ ] **Step 3: Service** — add to `allocationsService`:

```typescript
	updatePosition: async (id: string, x: number, y: number): Promise<void> => {
		await apiClient.put(`/allocations/projects/${id}/position`, { x, y });
	},
	createConnection: async (sourceProjectId: string, targetProjectId: string, label?: string): Promise<ProjectConnection> =>
		(await apiClient.post<ProjectConnection>('/allocations/connections', { sourceProjectId, targetProjectId, label })).data,
	removeConnection: async (id: string): Promise<void> => {
		await apiClient.delete(`/allocations/connections/${id}`);
	},
```
(import `ProjectConnection` in the type import block.)

- [ ] **Step 4: Verify + commit**

```bash
npx tsc --noEmit
git add package.json package-lock.json src/types/allocations.types.ts src/services/allocations.service.ts
git commit -m "feat(allocation): reactflow dep, canvas types + service"
```

---

## Task 5: Frontend — ProjectNode + React Flow board page

**Files:**
- Create: `src/pages/allocations/project-node.tsx`
- Rewrite: `src/pages/allocations/allocation-board-page.tsx`

Reference the React Flow v11 usage in `MentalHealthFrontend/.../workflow/WorkflowBuilder.tsx`.

- [ ] **Step 1: Create `project-node.tsx`** — custom node rendering the box + chips + handles + native drop zone:

```tsx
import { memo } from 'react';
import { Handle, Position, type NodeProps } from 'reactflow';
import { useTranslation } from 'react-i18next';
import { Settings2, X } from 'lucide-react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { cn } from '@/lib/utils';
import type { ProjectWithAllocations, Allocation } from '@/types/allocations.types';

export interface ProjectNodeData {
	project: ProjectWithAllocations;
	isStaff: boolean;
	onEdit: (project: ProjectWithAllocations) => void;
	onDropPerson: (projectId: string, payload: string) => void;
	onRemove: (allocationId: string) => void;
	onSaveNote: (allocationId: string, note: string) => void;
}

function initials(name: string) {
	return name.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
}

export const ProjectNode = memo(({ data }: NodeProps<ProjectNodeData>) => {
	const { t } = useTranslation();
	const { project, isStaff, onEdit, onDropPerson, onRemove, onSaveNote } = data;

	return (
		<div
			className="w-64 rounded-lg border bg-card shadow-sm"
			onDragOver={e => { e.preventDefault(); }}
			onDrop={e => { e.preventDefault(); const p = e.dataTransfer.getData('text/plain'); if (p) onDropPerson(project.id, p); }}
		>
			<Handle type="target" position={Position.Left} />
			<Handle type="source" position={Position.Right} />
			<div className="h-1 rounded-t-lg" style={{ backgroundColor: project.colorHex }} />
			<div className="flex items-center justify-between px-3 py-2">
				<div className="flex items-center gap-2 min-w-0">
					<span className="font-medium text-sm truncate">{project.title}</span>
					<span className="text-xs text-muted-foreground">{project.allocations.length}</span>
				</div>
				{isStaff && (
					<button className="nodrag text-muted-foreground hover:text-foreground" aria-label={t('allocations.editProjectAria')} onClick={() => onEdit(project)}>
						<Settings2 className="h-4 w-4" />
					</button>
				)}
			</div>
			<div className="flex flex-col gap-1 p-2 min-h-[60px]">
				{project.allocations.map(a => (
					<ChipRow key={a.id} allocation={a} onRemove={onRemove} onSaveNote={onSaveNote} />
				))}
				{project.allocations.length === 0 && (
					<p className="text-xs text-muted-foreground border border-dashed rounded-md p-3 text-center">{t('allocations.dropHere')}</p>
				)}
			</div>
		</div>
	);
});

function ChipRow({ allocation, onRemove, onSaveNote }: { allocation: Allocation; onRemove: (id: string) => void; onSaveNote: (id: string, note: string) => void; }) {
	const { t } = useTranslation();
	return (
		<div
			className="nodrag rounded-md border bg-background px-2 py-1"
			draggable
			onDragStart={e => { e.dataTransfer.setData('text/plain', `alloc:${allocation.id}`); }}
		>
			<div className="flex items-center gap-2">
				<Avatar className="h-6 w-6">
					{allocation.collaborator.photoUrl && <AvatarImage src={allocation.collaborator.photoUrl} alt={allocation.collaborator.fullName} />}
					<AvatarFallback>{initials(allocation.collaborator.fullName)}</AvatarFallback>
				</Avatar>
				<span className="text-sm truncate flex-1">{allocation.collaborator.fullName}</span>
				<button className="nodrag text-muted-foreground hover:text-destructive" aria-label={t('allocations.removeAria')} onClick={() => onRemove(allocation.id)}>
					<X className="h-3.5 w-3.5" />
				</button>
			</div>
			<input
				defaultValue={allocation.note ?? ''}
				placeholder={t('allocations.addNote')}
				onBlur={e => { if (e.target.value !== (allocation.note ?? '')) onSaveNote(allocation.id, e.target.value); }}
				className="nodrag mt-1 w-full bg-transparent text-xs italic text-muted-foreground outline-none border-b border-transparent focus:border-border"
			/>
		</div>
	);
}
```

> The note field is a raw `<input>` (a node-internal control on a canvas; the repo has no special input requirement inside React Flow nodes). `nodrag` prevents React Flow from dragging the node when interacting with chips/inputs/buttons.

- [ ] **Step 2: Rewrite `allocation-board-page.tsx`** as a React Flow canvas:

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import ReactFlow, {
	Background, Controls, MiniMap, Panel, ReactFlowProvider,
	useNodesState, useEdgesState, addEdge,
	type Connection, type Edge, type Node, type NodeDragHandler, type EdgeChange,
} from 'reactflow';
import 'reactflow/dist/style.css';
import { Plus, Search } from 'lucide-react';
import { allocationsService } from '@/services/allocations.service';
import { useAuth } from '@/hooks/use-auth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ProjectEditDialog } from './project-edit-dialog';
import { ProjectNode, type ProjectNodeData } from './project-node';
import type { AllocationBoard, CollaboratorChip, ProjectWithAllocations } from '@/types/allocations.types';

const nodeTypes = { project: ProjectNode };

function BoardInner() {
	const { t } = useTranslation();
	const qc = useQueryClient();
	const { me } = useAuth();
	const isStaff = me?.roleKind === 'Admin' || me?.roleKind === 'HR';

	const { data, isLoading } = useQuery({ queryKey: ['allocation-board'], queryFn: () => allocationsService.getBoard() });
	const board: AllocationBoard = data ?? { projects: [], collaborators: [], connections: [] };

	const [search, setSearch] = useState('');
	const [dialogOpen, setDialogOpen] = useState(false);
	const [editProject, setEditProject] = useState<ProjectWithAllocations | null>(null);

	const [nodes, setNodes, onNodesChange] = useNodesState<ProjectNodeData>([]);
	const [edges, setEdges, onEdgesChange] = useEdgesState([]);

	const invalidate = () => qc.invalidateQueries({ queryKey: ['allocation-board'] });
	const allocate = useMutation({ mutationFn: (a: { projectId: string; collaboratorId: string }) => allocationsService.allocate(a.projectId, a.collaboratorId), onSuccess: invalidate });
	const move = useMutation({ mutationFn: (a: { id: string; projectId: string }) => allocationsService.move(a.id, a.projectId, 0), onSuccess: invalidate });
	const remove = useMutation({ mutationFn: (id: string) => allocationsService.remove(id), onSuccess: invalidate });
	const saveNote = useMutation({ mutationFn: (a: { id: string; note: string }) => allocationsService.updateNote(a.id, a.note), onSuccess: invalidate });
	const savePosition = useMutation({ mutationFn: (a: { id: string; x: number; y: number }) => allocationsService.updatePosition(a.id, a.x, a.y) });
	const createConn = useMutation({ mutationFn: (a: { s: string; t: string }) => allocationsService.createConnection(a.s, a.t), onSuccess: invalidate });
	const removeConn = useMutation({ mutationFn: (id: string) => allocationsService.removeConnection(id), onSuccess: invalidate });

	const onDropPerson = useCallback((projectId: string, payload: string) => {
		if (payload.startsWith('pool:')) allocate.mutate({ projectId, collaboratorId: payload.slice(5) });
		else if (payload.startsWith('alloc:')) move.mutate({ id: payload.slice(6), projectId });
	}, [allocate, move]);

	const onEdit = useCallback((p: ProjectWithAllocations) => { setEditProject(p); setDialogOpen(true); }, []);

	// Rebuild nodes/edges from server data
	useEffect(() => {
		setNodes(board.projects.map(p => ({
			id: p.id,
			type: 'project',
			position: { x: p.positionX, y: p.positionY },
			data: { project: p, isStaff, onEdit, onDropPerson, onRemove: (id: string) => remove.mutate(id), onSaveNote: (id: string, note: string) => saveNote.mutate({ id, note }) },
		})));
		setEdges(board.connections.map(c => ({ id: c.id, source: c.sourceProjectId, target: c.targetProjectId, label: c.label ?? undefined })));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [data, isStaff]);

	const onNodeDragStop: NodeDragHandler = useCallback((_e, node) => {
		savePosition.mutate({ id: node.id, x: node.position.x, y: node.position.y });
	}, [savePosition]);

	const onConnect = useCallback((c: Connection) => {
		if (!c.source || !c.target || c.source === c.target) return;
		setEdges(eds => addEdge(c, eds));
		createConn.mutate({ s: c.source, t: c.target });
	}, [createConn, setEdges]);

	const onEdgesDelete = useCallback((deleted: Edge[]) => {
		deleted.forEach(e => removeConn.mutate(e.id));
	}, [removeConn]);

	const filteredPool = useMemo(
		() => board.collaborators.filter(c => c.fullName.toLowerCase().includes(search.toLowerCase())),
		[board.collaborators, search],
	);
	const allocatedIds = useMemo(() => new Set(board.projects.flatMap(p => p.allocations.map(a => a.collaborator.id))), [board.projects]);

	if (isLoading) return <p className="text-muted-foreground text-sm p-6">{t('common.loading')}</p>;

	return (
		<div className="flex h-full">
			<aside className="w-60 shrink-0 border-r flex flex-col gap-2 p-3">
				<h2 className="text-sm font-semibold">{t('allocations.people')}</h2>
				<div className="relative">
					<Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
					<Input className="pl-8" value={search} onChange={e => setSearch(e.target.value)} placeholder={t('allocations.searchPeople')} />
				</div>
				<div className="flex flex-col gap-1 overflow-y-auto">
					{filteredPool.map(c => <PoolChip key={c.id} c={c} unallocated={!allocatedIds.has(c.id)} unallocatedLabel={t('allocations.unallocated')} />)}
				</div>
			</aside>

			<div className="flex-1 h-full">
				<ReactFlow
					nodes={nodes}
					edges={edges}
					onNodesChange={onNodesChange}
					onEdgesChange={onEdgesChange as (c: EdgeChange[]) => void}
					onNodeDragStop={onNodeDragStop}
					onConnect={onConnect}
					onEdgesDelete={onEdgesDelete}
					nodeTypes={nodeTypes}
					nodesDraggable={isStaff}
					nodesConnectable={isStaff}
					elementsSelectable={isStaff}
					fitView
				>
					<Background />
					<Controls />
					<MiniMap pannable zoomable />
					{isStaff && (
						<Panel position="top-right">
							<Button size="sm" onClick={() => { setEditProject(null); setDialogOpen(true); }}>
								<Plus className="h-4 w-4 mr-1" />{t('allocations.newProject')}
							</Button>
						</Panel>
					)}
				</ReactFlow>
			</div>

			{dialogOpen && (
				<ProjectEditDialog key={editProject?.id ?? 'new'} open onOpenChange={setDialogOpen} project={editProject} />
			)}
		</div>
	);
}

function PoolChip({ c, unallocated, unallocatedLabel }: { c: CollaboratorChip; unallocated: boolean; unallocatedLabel: string }) {
	return (
		<div
			className="flex items-center gap-2 rounded-md border px-2 py-1 cursor-grab bg-card"
			draggable
			onDragStart={e => { e.dataTransfer.setData('text/plain', `pool:${c.id}`); }}
		>
			<span className="text-sm truncate flex-1">{c.fullName}</span>
			{unallocated && <span className="h-2 w-2 rounded-full bg-amber-500" title={unallocatedLabel} />}
		</div>
	);
}

export function AllocationBoardPage() {
	return (
		<ReactFlowProvider>
			<BoardInner />
		</ReactFlowProvider>
	);
}
```

> The page must fill height — the route renders inside the app layout. If the canvas has zero height, wrap usage so the parent provides height; `h-full` on the flex container + the layout's content area should suffice (verify visually). If not, set the canvas div to a concrete height like `h-[calc(100vh-4rem)]`.

- [ ] **Step 3: Verify**

```bash
npx tsc --noEmit
```
Fix any real type errors (e.g., `onEdgesChange` typing). No `any` (the cast on `onEdgesChange` is acceptable; prefer the correct generic if it resolves cleanly).

- [ ] **Step 4: Commit** — `git commit -am "feat(allocation): react flow canvas board + project node"`

---

## Task 6: Frontend — i18n + build + verify

**Files:**
- Modify: `src/locales/{pt-BR,en,es}/common.json`

- [ ] **Step 1: i18n** — the canvas reuses existing `allocations.*` keys. Confirm `allocations.dropHere`, `allocations.addNote`, `allocations.editProjectAria`, `allocations.removeAria`, `allocations.people`, `allocations.searchPeople`, `allocations.unallocated`, `allocations.newProject` all exist in all 3 locales (added by the prior feature). No new keys are strictly required. If you add an edge-label affordance later, add keys then. Run:

```bash
node -e "['pt-BR','en','es'].forEach(l=>{const o=require('./src/locales/'+l+'/common.json');['dropHere','addNote','editProjectAria','removeAria','people','searchPeople','unallocated','newProject'].forEach(k=>{if(!o.allocations||!(k in o.allocations))throw new Error(l+' missing allocations.'+k)})});console.log('i18n OK')"
```

- [ ] **Step 2: Build**

```bash
npx tsc --noEmit && npm run build
```
Expected: no type errors, build succeeds.

- [ ] **Step 3: Commit (if any locale changes)** — otherwise skip. `git commit -am "chore(allocation): i18n check for canvas"`

---

## Task 7: Manual verification

- [ ] Backend `dotnet run --project src/Waao.API`, frontend `npm run dev`.
- [ ] As Admin at `/allocations`: see canvas; create a project (Panel button) → node appears; drag node → reload → position persists; drag from node A handle to node B → edge appears → reload → edge persists; select edge + Delete → edge removed; drag a person from the pool onto a node → allocated; drag a chip from node A to node B → moved; edit note → persists; remove (X) → gone.
- [ ] As non-admin: canvas read-only (can't move/connect nodes) but can still drop people into nodes and edit notes.

---

## Deploy (on user request)
- Merge both branches to `main`, push. Backend GH Action deploys to Fly (migration auto-applies on startup); waao-api stays **1 machine**. Frontend: after push, **`npm run deploy`** (Cloudflare).

## Self-review notes
- Spec coverage: positions (T1/T2/T5), connections (T1/T2/T3/T5), admin-gated layout (T3 policy + T5 `nodesDraggable/Connectable=isStaff`), people-drop via native DnD (T5 `onDropPerson`/`dataTransfer` + `nodrag`), persist-everything (position + connection endpoints), reactflow v11 (T4). 
- Type consistency: service methods `updatePosition/createConnection/removeConnection` match T5 calls; DTO fields `positionX/positionY`, `connections`, `sourceProjectId/targetProjectId/label` consistent backend↔frontend.
- Flagged: canvas height (T5 note); `onEdgesChange` cast (T5 step 3).
