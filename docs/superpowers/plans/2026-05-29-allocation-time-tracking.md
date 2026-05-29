# Allocation Time Tracking (event audit) Plan

> Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** Record every allocation change (assign / move / remove) as an audited event with timestamp + actor, so we can show how long each person has been on a project (live + historical) — a duration badge on each chip and a per-person history panel.

**Architecture:** New append-only `ProjectAllocationEvent` log (`Assigned`/`Unassigned` per collaborator+project, with snapshot project title + actor). `AllocationService` writes events on allocate/move/remove. A history endpoint derives per-project totals (pair Assigned→Unassigned; open pair = active to now) + the raw timeline. Frontend shows a live duration badge (from existing `AllocatedAt`) and a history dialog.

**Tech Stack:** .NET 9 / EF Core / FluentValidation / xUnit. React 19, react-query, react-i18next.

**Golden patterns:** existing `AllocationService`, `AllocationsController`, `project-edit-dialog.tsx` (createPortal dialog), `allocations.service.ts`.

---

## Task 1: Backend — event entity, emission, history endpoint, tests

**Files:**
- Create `src/Waao.Domain.Models/Enums/AllocationEventType.cs`
- Create `src/Waao.Domain.Models/Entities/Allocation/ProjectAllocationEvent.cs`
- Modify `src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs`
- Modify `src/Waao.Infra.EF/WaaoDbContext.cs` (DbSet)
- Migration `AddAllocationEvents`
- Modify `src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs`
- Modify `src/Waao.Services.Abstractions/Services/IAllocationService.cs`
- Modify `src/Waao.Services/Mappers/AllocationMapper.cs`
- Modify `src/Waao.Services/Services/Allocation/AllocationService.cs`
- Modify `src/Waao.API/Controllers/AllocationsController.cs`
- Modify `tests/Waao.Tests/Allocation/AllocationServiceTests.cs`

- [ ] **Step 1: Enum** — `AllocationEventType.cs`:

```csharp
namespace Waao.Domain.Models.Enums;

public enum AllocationEventType
{
	Assigned,
	Unassigned,
}
```

- [ ] **Step 2: Entity** — `ProjectAllocationEvent.cs` (append-only audit row; snapshot title so archived/renamed projects still read correctly):

```csharp
using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Allocation;

public class ProjectAllocationEvent : Entity
{
	public Guid CollaboratorId { get; set; }
	public Guid ProjectId { get; set; }
	public string ProjectTitle { get; set; } = string.Empty;
	public AllocationEventType EventType { get; set; }
	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
	public Guid? ActorId { get; set; }
}
```

- [ ] **Step 3: EF config** — append to `AllocationConfiguration.cs`:

```csharp
public class ProjectAllocationEventConfiguration : IEntityTypeConfiguration<ProjectAllocationEvent>
{
	public void Configure(EntityTypeBuilder<ProjectAllocationEvent> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ProjectTitle).IsRequired().HasMaxLength(120);
		builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(20);
		builder.HasIndex(x => new { x.CollaboratorId, x.OccurredAt });
		builder.HasIndex(x => new { x.ProjectId, x.OccurredAt });
		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
```

- [ ] **Step 4: DbSet** — in `WaaoDbContext.cs` after `ProjectConnections`:

```csharp
	public DbSet<Waao.Domain.Models.Entities.Allocation.ProjectAllocationEvent> ProjectAllocationEvents => Set<Waao.Domain.Models.Entities.Allocation.ProjectAllocationEvent>();
```

- [ ] **Step 5: Migration**

```bash
dotnet build src/Waao.Infra.EF/Waao.Infra.EF.csproj
dotnet ef migrations add AddAllocationEvents -p src/Waao.Infra.EF -s src/Waao.API
dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API
```
Review: creates `project_allocation_events` with the two composite indexes, `event_type` as string, no DROPs.

- [ ] **Step 6: DTOs** — append to `AllocationDtos.cs`:

```csharp
public record AllocationEventDto
{
	public Guid Id { get; init; }
	public Guid ProjectId { get; init; }
	public string ProjectTitle { get; init; } = string.Empty;
	public string EventType { get; init; } = string.Empty;
	public DateTime OccurredAt { get; init; }
	public string? ActorName { get; init; }
}

public record ProjectTimeSummaryDto
{
	public Guid ProjectId { get; init; }
	public string ProjectTitle { get; init; } = string.Empty;
	public long TotalMinutes { get; init; }
	public int StintCount { get; init; }
	public bool Active { get; init; }
}

public record CollaboratorAllocationHistoryDto
{
	public Guid CollaboratorId { get; init; }
	public string FullName { get; init; } = string.Empty;
	public IReadOnlyList<ProjectTimeSummaryDto> Summary { get; init; } = [];
	public IReadOnlyList<AllocationEventDto> Events { get; init; } = [];
}
```

- [ ] **Step 7: Interface** — add to `IAllocationService`:

```csharp
	Task<CollaboratorAllocationHistoryDto> GetCollaboratorHistoryAsync(Guid collaboratorId, CancellationToken ct = default);
```

- [ ] **Step 8: Mapper** — add to `AllocationMapper`:

```csharp
	public static AllocationEventDto ToDto(ProjectAllocationEvent e, string? actorName) => new()
	{
		Id = e.Id,
		ProjectId = e.ProjectId,
		ProjectTitle = e.ProjectTitle,
		EventType = e.EventType.ToString(),
		OccurredAt = e.OccurredAt,
		ActorName = actorName,
	};
```

- [ ] **Step 9: Service** — emit events + history. Add a private helper and call it from the mutation methods.

Add helper inside `AllocationService`:

```csharp
	private void RecordEvent(Guid collaboratorId, Project project, AllocationEventType type, Guid? actorId)
		=> Db.ProjectAllocationEvents.Add(new ProjectAllocationEvent
		{
			Id = Guid.CreateVersion7(),
			CollaboratorId = collaboratorId,
			ProjectId = project.Id,
			ProjectTitle = project.Title,
			EventType = type,
			OccurredAt = DateTime.UtcNow,
			ActorId = actorId,
		});
```

In `AllocateAsync` — after creating the NEW allocation (the `Db.ProjectAllocations.Add(alloc)` path, NOT the idempotent-existing path), before `SaveChangesAsync`:

```csharp
		RecordEvent(dto.CollaboratorId, project, AllocationEventType.Assigned, currentCollaboratorId);
```

In `MoveAllocationAsync`:
- Load the source project for the title: at the top after loading `alloc`, get `var sourceProject = await Db.Projects.FirstOrDefaultAsync(p => p.Id == alloc.ProjectId, ct);` (alloc.ProjectId is the source before reassignment). `target` is already loaded.
- Merge/clash branch (collaborator already in target): record `Unassigned(sourceProject)` for `alloc.CollaboratorId` before `SaveChangesAsync`.
- Normal move branch (`dto.ProjectId != alloc.ProjectId`): record `Unassigned(sourceProject)` + `Assigned(target)` for `alloc.CollaboratorId`.
- (If `dto.ProjectId == alloc.ProjectId`, i.e. just a reposition, record nothing.)
Guard `sourceProject` null with `if (sourceProject != null)`.

In `RemoveAllocationAsync` — load the project and record Unassigned. Note this method currently doesn't take an actor; keep its signature, pass `actorId: null` (or thread an actor if easy — keep null to avoid signature churn):

```csharp
	public async Task RemoveAllocationAsync(Guid allocationId, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == alloc.ProjectId, ct);
		if (project != null)
			RecordEvent(alloc.CollaboratorId, project, AllocationEventType.Unassigned, null);
		alloc.IsDeleted = true;
		alloc.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
```

Add `GetCollaboratorHistoryAsync`:

```csharp
	public async Task<CollaboratorAllocationHistoryDto> GetCollaboratorHistoryAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var collaborator = await Db.Collaborators.FirstOrDefaultAsync(c => c.Id == collaboratorId, ct)
			?? throw new KeyNotFoundException($"Collaborator {collaboratorId} not found.");

		var events = await Db.ProjectAllocationEvents
			.AsNoTracking()
			.Where(e => e.CollaboratorId == collaboratorId)
			.OrderBy(e => e.OccurredAt)
			.ToListAsync(ct);

		var actorIds = events.Where(e => e.ActorId.HasValue).Select(e => e.ActorId!.Value).Distinct().ToList();
		var actors = await Db.Collaborators.AsNoTracking()
			.Where(c => actorIds.Contains(c.Id))
			.ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

		// Derive per-project durations: walk events chronologically; Assigned opens a stint,
		// Unassigned closes the most recent open one. Open stint at the end = active (to now).
		var now = DateTime.UtcNow;
		var summary = events
			.GroupBy(e => new { e.ProjectId, e.ProjectTitle })
			.Select(g =>
			{
				long minutes = 0;
				int stints = 0;
				DateTime? openedAt = null;
				foreach (var e in g.OrderBy(x => x.OccurredAt))
				{
					if (e.EventType == AllocationEventType.Assigned)
					{
						openedAt ??= e.OccurredAt;
					}
					else if (e.EventType == AllocationEventType.Unassigned && openedAt is { } start)
					{
						minutes += (long)(e.OccurredAt - start).TotalMinutes;
						stints++;
						openedAt = null;
					}
				}
				var active = openedAt is { } o;
				if (openedAt is { } o2)
				{
					minutes += (long)(now - o2).TotalMinutes;
					stints++;
				}
				return new ProjectTimeSummaryDto
				{
					ProjectId = g.Key.ProjectId,
					ProjectTitle = g.Key.ProjectTitle,
					TotalMinutes = minutes,
					StintCount = stints,
					Active = active,
				};
			})
			.OrderByDescending(s => s.Active).ThenByDescending(s => s.TotalMinutes)
			.ToList();

		return new CollaboratorAllocationHistoryDto
		{
			CollaboratorId = collaboratorId,
			FullName = collaborator.FullName,
			Summary = summary,
			Events = events.OrderByDescending(e => e.OccurredAt)
				.Select(e => AllocationMapper.ToDto(e, e.ActorId.HasValue && actors.TryGetValue(e.ActorId.Value, out var n) ? n : null))
				.ToList(),
		};
	}
```

- [ ] **Step 10: Controller** — add (any authenticated):

```csharp
	[HttpGet("history/{collaboratorId:guid}")]
	[ProducesResponseType(typeof(CollaboratorAllocationHistoryDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetHistory(Guid collaboratorId, CancellationToken ct)
		=> Ok(await Service.GetCollaboratorHistoryAsync(collaboratorId, ct));
```

- [ ] **Step 11: Tests** — add to `AllocationServiceTests.cs` (Build() unchanged — no new validators):

```csharp
	[Fact]
	public async Task Allocate_RecordsAssignedEvent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Eva");
		var p = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P" });

		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p.Id, CollaboratorId = who }, who);

		var hist = await svc.GetCollaboratorHistoryAsync(who);
		hist.Events.Should().ContainSingle(e => e.EventType == "Assigned" && e.ProjectId == p.Id);
		hist.Summary.Should().ContainSingle(s => s.ProjectId == p.Id && s.Active);
	}

	[Fact]
	public async Task Move_RecordsUnassignedAndAssigned()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Fred");
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P1" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P2" });
		var a = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p1.Id, CollaboratorId = who }, who);

		await svc.MoveAllocationAsync(a.Id, new MoveAllocationDto { ProjectId = p2.Id, Position = 0 });

		var hist = await svc.GetCollaboratorHistoryAsync(who);
		hist.Events.Should().Contain(e => e.EventType == "Unassigned" && e.ProjectId == p1.Id);
		hist.Events.Should().Contain(e => e.EventType == "Assigned" && e.ProjectId == p2.Id);
	}

	[Fact]
	public async Task Remove_RecordsUnassignedEvent_AndStintNotActive()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Gina");
		var p = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P" });
		var a = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p.Id, CollaboratorId = who }, who);

		await svc.RemoveAllocationAsync(a.Id);

		var hist = await svc.GetCollaboratorHistoryAsync(who);
		hist.Events.Count(e => e.ProjectId == p.Id).Should().Be(2); // Assigned + Unassigned
		hist.Summary.Single(s => s.ProjectId == p.Id).Active.Should().BeFalse();
	}
```

> `SeedCollaborator` helper already exists in the test file (used by earlier tests). If not, add one mirroring the existing seed pattern.

- [ ] **Step 12: Build + test**

```bash
dotnet build src/Waao.API/Waao.API.csproj
dotnet test tests/Waao.Tests/Waao.Tests.csproj --filter "FullyQualifiedName~AllocationServiceTests"
```
Expect all prior + 3 new passing.

- [ ] **Step 13: Commit** — `git add src/ tests/ && git commit -m "feat(allocation): allocation event audit log + collaborator history"`

---

## Task 2: Frontend — duration badge + history dialog

**Files:**
- Modify `src/types/allocations.types.ts`
- Modify `src/services/allocations.service.ts`
- Create `src/lib/format-duration.ts`
- Modify `src/pages/allocations/project-node.tsx` (badge + history button)
- Create `src/pages/allocations/allocation-history-dialog.tsx`
- Modify `src/pages/allocations/allocation-board-page.tsx` (wire history dialog)
- Modify `src/locales/{pt-BR,en,es}/common.json`

- [ ] **Step 1: Types** — add:

```typescript
export interface AllocationEvent {
	id: string;
	projectId: string;
	projectTitle: string;
	eventType: 'Assigned' | 'Unassigned';
	occurredAt: string;
	actorName?: string | null;
}

export interface ProjectTimeSummary {
	projectId: string;
	projectTitle: string;
	totalMinutes: number;
	stintCount: number;
	active: boolean;
}

export interface CollaboratorAllocationHistory {
	collaboratorId: string;
	fullName: string;
	summary: ProjectTimeSummary[];
	events: AllocationEvent[];
}
```

- [ ] **Step 2: Service** — add:

```typescript
	getHistory: async (collaboratorId: string): Promise<CollaboratorAllocationHistory> =>
		(await apiClient.get<CollaboratorAllocationHistory>(`/allocations/history/${collaboratorId}`)).data,
```
(import the new type.)

- [ ] **Step 3: Duration util** — `src/lib/format-duration.ts`:

```typescript
// "3d 4h" | "5h" | "12m" | "agora". Compact, largest two units.
export function formatDurationSince(iso: string, now: number = Date.now()): string {
	const ms = Math.max(0, now - new Date(iso).getTime());
	return formatDurationMs(ms);
}

export function formatDurationMinutes(totalMinutes: number): string {
	return formatDurationMs(totalMinutes * 60_000);
}

function formatDurationMs(ms: number): string {
	const m = Math.floor(ms / 60_000);
	if (m < 1) return 'agora';
	const days = Math.floor(m / 1440);
	const hours = Math.floor((m % 1440) / 60);
	const mins = m % 60;
	if (days > 0) return hours > 0 ? `${days}d ${hours}h` : `${days}d`;
	if (hours > 0) return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
	return `${mins}m`;
}
```
> Note: `Date.now()` default is fine in app runtime (this is the browser, not a workflow script). For i18n of "agora", optionally accept a label — keep simple for v1.

- [ ] **Step 4: ProjectNode chip** — in `ChipRow`, add a live duration badge next to the name and a small history (clock) button. Add `onShowHistory: (collaboratorId: string) => void` to `ProjectNodeData` (and pass through to ChipRow). Imports: `Clock` from lucide, `formatDurationSince` from `@/lib/format-duration`. In the chip name row:

```tsx
				<span className="text-sm truncate flex-1">{allocation.collaborator.fullName}</span>
				<span className="text-[10px] font-mono text-muted-foreground shrink-0" title={t('allocations.timeOnProject')}>
					{formatDurationSince(allocation.allocatedAt)}
				</span>
				<button className="nodrag text-muted-foreground hover:text-foreground" aria-label={t('allocations.viewHistory')} onClick={() => onShowHistory(allocation.collaborator.id)}>
					<Clock className="h-3.5 w-3.5" />
				</button>
				<button className="nodrag text-muted-foreground hover:text-destructive" aria-label={t('allocations.removeAria')} onClick={() => onRemove(allocation.id)}>
					<X className="h-3.5 w-3.5" />
				</button>
```
Thread `onShowHistory` from `ProjectNode` props → `ChipRow`.

- [ ] **Step 5: History dialog** — `allocation-history-dialog.tsx` (createPortal, mirrors `project-edit-dialog.tsx` structure):

```tsx
import { createPortal } from 'react-dom';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { allocationsService } from '@/services/allocations.service';
import { formatDurationMinutes } from '@/lib/format-duration';

interface Props {
	collaboratorId: string;
	onClose: () => void;
}

export function AllocationHistoryDialog({ collaboratorId, onClose }: Props) {
	const { t, i18n } = useTranslation();
	const { data, isLoading } = useQuery({
		queryKey: ['allocation-history', collaboratorId],
		queryFn: () => allocationsService.getHistory(collaboratorId),
	});

	useEffect(() => {
		const prev = document.body.style.overflow;
		document.body.style.overflow = 'hidden';
		return () => { document.body.style.overflow = prev; };
	}, []);

	const fmtDate = (iso: string) => new Date(iso).toLocaleString(i18n.language);

	return createPortal(
		<div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-8" role="dialog" aria-modal="true">
			<button onClick={onClose} className="absolute inset-0 bg-foreground/70 backdrop-blur-md cursor-default" aria-label={t('common.close')} />
			<div className="relative w-full max-w-lg max-h-full bg-card border rounded-lg shadow-2xl flex flex-col">
				<div className="border-b px-6 py-5">
					<h2 className="text-lg font-semibold tracking-tight">{t('allocations.historyTitle')}{data ? ` — ${data.fullName}` : ''}</h2>
				</div>
				<div className="p-6 space-y-6 overflow-y-auto max-h-[calc(100vh-12rem)]">
					{isLoading && <p className="text-sm text-muted-foreground">{t('common.loading')}</p>}
					{data && (
						<>
							<section className="space-y-2">
								<h3 className="text-sm font-medium">{t('allocations.timePerProject')}</h3>
								{data.summary.length === 0 && <p className="text-xs text-muted-foreground">{t('allocations.noHistory')}</p>}
								{data.summary.map(s => (
									<div key={s.projectId} className="flex items-center justify-between text-sm border-b py-1">
										<span className="flex items-center gap-2">
											{s.active && <span className="h-2 w-2 rounded-full bg-emerald-500" />}
											{s.projectTitle}
										</span>
										<span className="font-mono text-muted-foreground">{formatDurationMinutes(s.totalMinutes)} · {s.stintCount}×</span>
									</div>
								))}
							</section>
							<section className="space-y-2">
								<h3 className="text-sm font-medium">{t('allocations.timeline')}</h3>
								{data.events.map(e => (
									<div key={e.id} className="flex items-center justify-between text-xs border-b py-1">
										<span>
											<span className={e.eventType === 'Assigned' ? 'text-emerald-500' : 'text-muted-foreground'}>
												{e.eventType === 'Assigned' ? t('allocations.assigned') : t('allocations.unassigned')}
											</span>
											{' '}{e.projectTitle}{e.actorName ? ` · ${e.actorName}` : ''}
										</span>
										<span className="font-mono text-muted-foreground">{fmtDate(e.occurredAt)}</span>
									</div>
								))}
							</section>
						</>
					)}
				</div>
			</div>
		</div>,
		document.body,
	);
}
```

- [ ] **Step 6: Wire into board page** — in `BoardInner`: add `const [historyFor, setHistoryFor] = useState<string | null>(null);`. Add `onShowHistory: (id: string) => setHistoryFor(id)` to each node's `data` (in the build effect, alongside the other callbacks). Render at the end (next to `ProjectEditDialog`):

```tsx
			{historyFor && <AllocationHistoryDialog collaboratorId={historyFor} onClose={() => setHistoryFor(null)} />}
```
Import `AllocationHistoryDialog`.

- [ ] **Step 7: i18n** — add to all 3 locales under `allocations`:
  - pt-BR: `timeOnProject`:"Tempo no projeto", `viewHistory`:"Ver histórico", `historyTitle`:"Histórico de alocação", `timePerProject`:"Tempo por projeto", `timeline`:"Linha do tempo", `assigned`:"Alocado em", `unassigned`:"Removido de", `noHistory`:"Sem histórico ainda"
  - en: "Time on project","View history","Allocation history","Time per project","Timeline","Assigned to","Removed from","No history yet"
  - es: "Tiempo en el proyecto","Ver historial","Historial de asignación","Tiempo por proyecto","Línea de tiempo","Asignado a","Eliminado de","Sin historial aún"
  Keep all 3 locales key-for-key in sync.

- [ ] **Step 8: Verify** — `npx tsc --noEmit` (no `any`), validate JSON, `npm run build`.

- [ ] **Step 9: Commit** — `git add src/ && git commit -m "feat(allocation): duration badge on chips + per-person history dialog"`

---

## Task 3: Manual verification
- Allocate a person → chip shows a small duration that grows over time. Move them to another box, remove them, re-add → click the clock icon → history dialog shows the timeline (Assigned/Removed events with timestamps) and per-project totals with stint counts; current project marked active.

## Deploy (on request)
Backend merge→push (Fly GH Action; migration auto-applies on startup; 1 machine). Frontend push + `npm run deploy`.

## Self-review notes
- Coverage: audit log (T1 entity/migration), events on allocate/move/remove (T1 service), history endpoint + durations (T1), badge (T2 chip), history panel (T2 dialog). 
- Type consistency: `getHistory`↔`history/{id}`; `eventType` string 'Assigned'|'Unassigned' matches backend `.ToString()`; `totalMinutes` long↔number.
- Commit-guard: stage explicit paths, never combine `git commit` with `.claude-flow`, no AI words in messages.
