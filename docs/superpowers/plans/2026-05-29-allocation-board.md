# Allocation Board ("Quem está em quê") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A board of configurable project "boxes" where any collaborator can be dragged into one or more boxes, each placement carrying a free-text note, so admins can see at a glance who is working on what.

**Architecture:** Two new soft-deletable entities — `Project` (the box) and `ProjectAllocation` (person-in-box, unique per project+collaborator). A single `AllocationService` exposes a board read + project CRUD (admin-gated via the existing `"Admin"` authorization policy) + allocation mutations (open to any collaborator). The React page reuses the existing `@dnd-kit` drag pattern from `board-view.tsx`: a searchable collaborator pool on the left, droppable project boxes in a responsive grid, draggable collaborator chips.

**Tech Stack:** .NET 9, EF Core (Npgsql, snake_case), FluentValidation, xUnit + FluentAssertions (EF InMemory). React 19, TypeScript, `@dnd-kit/core` + `@dnd-kit/sortable`, `@tanstack/react-query`, react-i18next, `@/components/ui`.

---

## File Structure

**Backend (`Waao/WaaoBackend/`):**
- Create `src/Waao.Domain.Models/Entities/Allocation/Project.cs` — Project + ProjectAllocation entities
- Create `src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs` — EF config for both
- Modify `src/Waao.Infra.EF/WaaoDbContext.cs` — two DbSets
- Create migration `src/Waao.Infra.EF/Migrations/*_AddAllocationBoard.cs` (generated)
- Create `src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs` — all DTOs
- Create `src/Waao.Services.Abstractions/Services/IAllocationService.cs`
- Create `src/Waao.Services/Validation/Allocation/AllocationValidators.cs`
- Create `src/Waao.Services/Mappers/AllocationMapper.cs`
- Create `src/Waao.Services/Services/Allocation/AllocationService.cs`
- Create `src/Waao.API/Controllers/AllocationsController.cs`
- Modify `src/Waao.API/Program.cs` — register service + validators
- Create `tests/Waao.Tests/Allocation/AllocationServiceTests.cs`

**Frontend (`Waao/WaaoFrontend/`):**
- Create `src/types/allocations.types.ts`
- Create `src/services/allocations.service.ts`
- Create `src/pages/allocations/allocation-board-page.tsx`
- Create `src/pages/allocations/project-edit-dialog.tsx`
- Modify `src/App.tsx` — route
- Modify `src/components/layout/sidebar.tsx` — nav item
- Modify `src/locales/{pt-BR,en,es}/common.json` — `allocations.*` keys

---

## Task 1: Domain entities

**Files:**
- Create: `src/Waao.Domain.Models/Entities/Allocation/Project.cs`

- [ ] **Step 1: Create the entities**

```csharp
namespace Waao.Domain.Models.Entities.Allocation;

public class Project : Entity
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#2A6B7E";
	public int Position { get; set; }
	public bool IsArchived { get; set; }

	public virtual ICollection<ProjectAllocation> Allocations { get; set; } = [];
}

public class ProjectAllocation : Entity
{
	public Guid ProjectId { get; set; }
	public virtual Project Project { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public string? Note { get; set; }
	public int Position { get; set; }
	public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
	public Guid? AllocatedById { get; set; }
}
```

- [ ] **Step 2: Build the Domain.Models project**

Run: `dotnet build src/Waao.Domain.Models/Waao.Domain.Models.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Waao.Domain.Models/Entities/Allocation/Project.cs
git commit -m "feat(allocation): add Project and ProjectAllocation entities"
```

---

## Task 2: EF configuration + DbSets

**Files:**
- Create: `src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs`
- Modify: `src/Waao.Infra.EF/WaaoDbContext.cs` (DbSet block after the Kanban DbSets ~line 65)

- [ ] **Step 1: Write the EF configuration** (mirrors `CourseConfiguration` / `CourseCompletionConfiguration`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Waao.Domain.Models.Entities.Allocation;

namespace Waao.Infra.EF.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
	public void Configure(EntityTypeBuilder<Project> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.Property(x => x.ColorHex).IsRequired().HasMaxLength(9);
		builder.Property(x => x.IsArchived).HasDefaultValue(false);

		builder.HasIndex(x => new { x.IsArchived, x.IsDeleted });

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}

public class ProjectAllocationConfiguration : IEntityTypeConfiguration<ProjectAllocation>
{
	public void Configure(EntityTypeBuilder<ProjectAllocation> builder)
	{
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Note).HasMaxLength(500);

		builder.HasOne(x => x.Project)
			.WithMany(p => p.Allocations)
			.HasForeignKey(x => x.ProjectId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Collaborator)
			.WithMany()
			.HasForeignKey(x => x.CollaboratorId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.ProjectId, x.CollaboratorId })
			.IsUnique()
			.HasFilter("is_deleted = false");

		builder.HasIndex(x => x.CollaboratorId);
		builder.HasIndex(x => x.ProjectId);

		builder.HasQueryFilter(x => !x.IsDeleted);
	}
}
```

- [ ] **Step 2: Add DbSets to `WaaoDbContext.cs`**

After the last Kanban DbSet (the `CardActivities` line, ~line 65), add:

```csharp

	// ----- Allocation board -----
	public DbSet<Waao.Domain.Models.Entities.Allocation.Project> Projects => Set<Waao.Domain.Models.Entities.Allocation.Project>();
	public DbSet<Waao.Domain.Models.Entities.Allocation.ProjectAllocation> ProjectAllocations => Set<Waao.Domain.Models.Entities.Allocation.ProjectAllocation>();
```

(Configs are auto-applied via `ApplyConfigurationsFromAssembly` already in `OnModelCreating` — no change needed there.)

- [ ] **Step 3: Build the Infra.EF project**

Run: `dotnet build src/Waao.Infra.EF/Waao.Infra.EF.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Waao.Infra.EF/Configurations/AllocationConfiguration.cs src/Waao.Infra.EF/WaaoDbContext.cs
git commit -m "feat(allocation): EF config and DbSets for Project/ProjectAllocation"
```

---

## Task 3: Migration

**Files:**
- Create (generated): `src/Waao.Infra.EF/Migrations/*_AddAllocationBoard.cs`

- [ ] **Step 1: Generate the migration**

Run from `WaaoBackend/`:
```bash
dotnet ef migrations add AddAllocationBoard -p src/Waao.Infra.EF -s src/Waao.API
```
Expected: a new migration + updated `WaaoDbContextModelSnapshot.cs`.

- [ ] **Step 2: Review the migration**

Open the generated `*_AddAllocationBoard.cs`. Verify:
- `projects` table: `title`, `description` (nullable), `color_hex`, `position`, `is_archived` (default false), plus base columns (`id`, `created_at`, `updated_at`, `is_deleted`, `deleted_at`).
- `project_allocations` table: `project_id`, `collaborator_id`, `note` (nullable), `position`, `allocated_at`, `allocated_by_id` (nullable), base columns.
- Unique index on `(project_id, collaborator_id)` with filter `is_deleted = false`.
- FK indexes on `project_id`, `collaborator_id`.
- No DROP statements.

- [ ] **Step 3: Apply to local dev DB**

Run from `WaaoBackend/`:
```bash
dotnet ef database update -p src/Waao.Infra.EF -s src/Waao.API
```
Expected: "Done." and the two tables created.

- [ ] **Step 4: Commit**

```bash
git add src/Waao.Infra.EF/Migrations/
git commit -m "feat(allocation): add AddAllocationBoard migration"
```

---

## Task 4: DTOs

**Files:**
- Create: `src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs`

- [ ] **Step 1: Write the DTOs** (records with `init` setters)

```csharp
namespace Waao.Services.Abstractions.Dtos.Allocation;

public record CollaboratorChipDto
{
	public Guid Id { get; init; }
	public string FullName { get; init; } = string.Empty;
	public string? PhotoUrl { get; init; }
	public string? RoleTitle { get; init; }
	public string? DepartmentName { get; init; }
}

public record AllocationDto
{
	public Guid Id { get; init; }
	public Guid ProjectId { get; init; }
	public string? Note { get; init; }
	public int Position { get; init; }
	public DateTime AllocatedAt { get; init; }
	public CollaboratorChipDto Collaborator { get; init; } = new();
}

public record ProjectWithAllocationsDto
{
	public Guid Id { get; init; }
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
	public int Position { get; init; }
	public IReadOnlyList<AllocationDto> Allocations { get; init; } = [];
}

public record AllocationBoardDto
{
	public IReadOnlyList<ProjectWithAllocationsDto> Projects { get; init; } = [];
	public IReadOnlyList<CollaboratorChipDto> Collaborators { get; init; } = [];
}

public record CreateProjectDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string? ColorHex { get; init; }
}

public record UpdateProjectDto
{
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string ColorHex { get; init; } = "#2A6B7E";
}

public record ReorderProjectsDto
{
	public IReadOnlyList<Guid> OrderedIds { get; init; } = [];
}

public record CreateAllocationDto
{
	public Guid ProjectId { get; init; }
	public Guid CollaboratorId { get; init; }
	public string? Note { get; init; }
}

public record MoveAllocationDto
{
	public Guid ProjectId { get; init; }
	public int Position { get; init; }
}

public record UpdateNoteDto
{
	public string? Note { get; init; }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Waao.Services.Abstractions/Waao.Services.Abstractions.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Waao.Services.Abstractions/Dtos/Allocation/AllocationDtos.cs
git commit -m "feat(allocation): add allocation DTOs"
```

---

## Task 5: Service interface

**Files:**
- Create: `src/Waao.Services.Abstractions/Services/IAllocationService.cs`

- [ ] **Step 1: Write the interface**

```csharp
using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Abstractions.Services;

public interface IAllocationService
{
	Task<AllocationBoardDto> GetBoardAsync(CancellationToken ct = default);
	Task<IReadOnlyList<ProjectWithAllocationsDto>> GetByCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default);

	Task<ProjectWithAllocationsDto> CreateProjectAsync(CreateProjectDto dto, CancellationToken ct = default);
	Task<ProjectWithAllocationsDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto, CancellationToken ct = default);
	Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default);
	Task ReorderProjectsAsync(ReorderProjectsDto dto, CancellationToken ct = default);

	Task<AllocationDto> AllocateAsync(CreateAllocationDto dto, Guid currentCollaboratorId, CancellationToken ct = default);
	Task<AllocationDto> MoveAllocationAsync(Guid allocationId, MoveAllocationDto dto, CancellationToken ct = default);
	Task<AllocationDto> UpdateNoteAsync(Guid allocationId, UpdateNoteDto dto, CancellationToken ct = default);
	Task RemoveAllocationAsync(Guid allocationId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Waao.Services.Abstractions/Waao.Services.Abstractions.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Waao.Services.Abstractions/Services/IAllocationService.cs
git commit -m "feat(allocation): add IAllocationService"
```

---

## Task 6: Validators

**Files:**
- Create: `src/Waao.Services/Validation/Allocation/AllocationValidators.cs`

- [ ] **Step 1: Write validators** (mirrors `CourseValidators`)

```csharp
using FluentValidation;
using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Validation.Allocation;

public class CreateProjectValidator : AbstractValidator<CreateProjectDto>
{
	public CreateProjectValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
		RuleFor(x => x.ColorHex).MaximumLength(9).When(x => x.ColorHex is not null);
	}
}

public class UpdateProjectValidator : AbstractValidator<UpdateProjectDto>
{
	public UpdateProjectValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
		RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
		RuleFor(x => x.ColorHex).NotEmpty().MaximumLength(9);
	}
}

public class CreateAllocationValidator : AbstractValidator<CreateAllocationDto>
{
	public CreateAllocationValidator()
	{
		RuleFor(x => x.ProjectId).NotEmpty();
		RuleFor(x => x.CollaboratorId).NotEmpty();
		RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
	}
}

public class UpdateNoteValidator : AbstractValidator<UpdateNoteDto>
{
	public UpdateNoteValidator()
	{
		RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
	}
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Waao.Services/Waao.Services.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Waao.Services/Validation/Allocation/AllocationValidators.cs
git commit -m "feat(allocation): add allocation validators"
```

---

## Task 7: Mapper

**Files:**
- Create: `src/Waao.Services/Mappers/AllocationMapper.cs`

- [ ] **Step 1: Write the mapper** (static, mirrors how `CourseService.ToDto` maps; kept in a dedicated mapper file per coding-style)

```csharp
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Services.Abstractions.Dtos.Allocation;

namespace Waao.Services.Mappers;

public static class AllocationMapper
{
	public static CollaboratorChipDto ToChip(Collaborator c) => new()
	{
		Id = c.Id,
		FullName = c.FullName,
		PhotoUrl = c.PhotoUrl,
		RoleTitle = c.Role != null ? c.Role.Title : null,
		DepartmentName = c.Department != null ? c.Department.Name : null,
	};

	public static AllocationDto ToDto(ProjectAllocation a) => new()
	{
		Id = a.Id,
		ProjectId = a.ProjectId,
		Note = a.Note,
		Position = a.Position,
		AllocatedAt = a.AllocatedAt,
		Collaborator = ToChip(a.Collaborator),
	};

	public static ProjectWithAllocationsDto ToDto(Project p) => new()
	{
		Id = p.Id,
		Title = p.Title,
		Description = p.Description,
		ColorHex = p.ColorHex,
		Position = p.Position,
		Allocations = p.Allocations
			.OrderBy(a => a.Position)
			.Select(ToDto)
			.ToList(),
	};
}
```

> Note: `Role.Title` and `Department.Name` — confirm these property names exist on `Role`/`Department` entities. If `Role` uses `Name` instead of `Title`, adjust `ToChip` accordingly (grep `class Role` / `class Department` in `Waao.Domain.Models`).

- [ ] **Step 2: Verify Role/Department property names**

Run: `grep -nE "public string (Title|Name)" src/Waao.Domain.Models/Entities/Role.cs src/Waao.Domain.Models/Entities/Department.cs`
If the property differs from `Title`/`Name`, update `ToChip` before building.

- [ ] **Step 3: Build**

Run: `dotnet build src/Waao.Services/Waao.Services.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Waao.Services/Mappers/AllocationMapper.cs
git commit -m "feat(allocation): add AllocationMapper"
```

---

## Task 8: Service implementation (TDD)

**Files:**
- Create: `src/Waao.Services/Services/Allocation/AllocationService.cs`
- Test: `tests/Waao.Tests/Allocation/AllocationServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using Xunit;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Services.Allocation;
using Waao.Services.Validation.Allocation;
using Waao.Tests.Support;

namespace Waao.Tests.Allocation;

public class AllocationServiceTests
{
	private static AllocationService Build(Waao.Infra.EF.WaaoDbContext db) =>
		new(db, new CreateProjectValidator(), new UpdateProjectValidator(),
			new CreateAllocationValidator(), new UpdateNoteValidator());

	private static async Task<Guid> SeedCollaborator(Waao.Infra.EF.WaaoDbContext db, string name)
	{
		var id = Guid.CreateVersion7();
		db.Collaborators.Add(new Collaborator
		{
			Id = id, FullName = name, Email = $"{id}@example.com",
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
		});
		await db.SaveChangesAsync();
		return id;
	}

	[Fact]
	public async Task CreateProject_ThenBoard_ReturnsProjectAndAllActiveCollaborators()
	{
		var db = TestDb.New();
		var svc = Build(db);
		await SeedCollaborator(db, "Alice");

		await svc.CreateProjectAsync(new CreateProjectDto { Title = "Billing" });
		var board = await svc.GetBoardAsync();

		board.Projects.Should().ContainSingle(p => p.Title == "Billing");
		board.Collaborators.Should().ContainSingle(c => c.FullName == "Alice");
	}

	[Fact]
	public async Task Allocate_SameCollaboratorTwiceToSameProject_IsIdempotent()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Bob");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Emergency" });

		var first  = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who, Note = "triage" }, who);
		var second = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who, Note = "still triage" }, who);

		second.Id.Should().Be(first.Id);
		var board = await svc.GetBoardAsync();
		board.Projects.Single().Allocations.Should().ContainSingle();
		board.Projects.Single().Allocations.Single().Note.Should().Be("still triage");
	}

	[Fact]
	public async Task Allocate_SameCollaboratorToTwoProjects_AppearsInBoth()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Cara");
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P1" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P2" });

		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p1.Id, CollaboratorId = who }, who);
		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p2.Id, CollaboratorId = who }, who);

		var board = await svc.GetBoardAsync();
		board.Projects.SelectMany(p => p.Allocations).Should().HaveCount(2);
	}

	[Fact]
	public async Task Move_ToProjectWhereCollaboratorAlreadyExists_MergesNoDuplicate()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Dan");
		var p1 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P1" });
		var p2 = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P2" });
		var inP1 = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p1.Id, CollaboratorId = who }, who);
		await svc.AllocateAsync(new CreateAllocationDto { ProjectId = p2.Id, CollaboratorId = who }, who);

		await svc.MoveAllocationAsync(inP1.Id, new MoveAllocationDto { ProjectId = p2.Id, Position = 0 });

		var board = await svc.GetBoardAsync();
		board.Projects.SelectMany(p => p.Allocations).Should().ContainSingle();
		board.Projects.Single(p => p.Id == p2.Id).Allocations.Should().ContainSingle();
	}

	[Fact]
	public async Task Allocate_ToArchivedProject_Throws()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Eve");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Old" });
		await svc.ArchiveProjectAsync(proj.Id);

		var act = () => svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who }, who);
		await act.Should().ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task Archive_HidesProjectFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "Temp" });

		await svc.ArchiveProjectAsync(proj.Id);

		var board = await svc.GetBoardAsync();
		board.Projects.Should().BeEmpty();
	}

	[Fact]
	public async Task RemoveAllocation_SoftDeletes_DisappearsFromBoard()
	{
		var db = TestDb.New();
		var svc = Build(db);
		var who = await SeedCollaborator(db, "Fay");
		var proj = await svc.CreateProjectAsync(new CreateProjectDto { Title = "P" });
		var alloc = await svc.AllocateAsync(new CreateAllocationDto { ProjectId = proj.Id, CollaboratorId = who }, who);

		await svc.RemoveAllocationAsync(alloc.Id);

		var board = await svc.GetBoardAsync();
		board.Projects.Single().Allocations.Should().BeEmpty();
	}
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Waao.Tests/Waao.Tests.csproj --filter "FullyQualifiedName~AllocationServiceTests"`
Expected: FAIL — `AllocationService` does not exist (compile error).

- [ ] **Step 3: Write the service implementation**

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Waao.Domain.Models.Entities;
using Waao.Domain.Models.Entities.Allocation;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Abstractions.Services;
using Waao.Services.Mappers;

namespace Waao.Services.Services.Allocation;

public sealed class AllocationService(
	WaaoDbContext Db,
	IValidator<CreateProjectDto> CreateProjectValidator,
	IValidator<UpdateProjectDto> UpdateProjectValidator,
	IValidator<CreateAllocationDto> CreateAllocationValidator,
	IValidator<UpdateNoteDto> UpdateNoteValidator) : IAllocationService
{
	public async Task<AllocationBoardDto> GetBoardAsync(CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.Where(p => !p.IsArchived)
			.OrderBy(p => p.Position).ThenBy(p => p.Title)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.ToListAsync(ct);

		var collaborators = await Db.Collaborators
			.Where(c => c.Status == Domain.Models.Enums.CollaboratorStatus.Active)
			.Include(c => c.Role)
			.Include(c => c.Department)
			.OrderBy(c => c.FullName)
			.ToListAsync(ct);

		return new AllocationBoardDto
		{
			Projects = projects.Select(AllocationMapper.ToDto).ToList(),
			Collaborators = collaborators.Select(AllocationMapper.ToChip).ToList(),
		};
	}

	public async Task<IReadOnlyList<ProjectWithAllocationsDto>> GetByCollaboratorAsync(Guid collaboratorId, CancellationToken ct = default)
	{
		var projects = await Db.Projects
			.Where(p => !p.IsArchived && p.Allocations.Any(a => a.CollaboratorId == collaboratorId))
			.OrderBy(p => p.Position)
			.Include(p => p.Allocations.Where(a => a.CollaboratorId == collaboratorId))
				.ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations.Where(a => a.CollaboratorId == collaboratorId))
				.ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.ToListAsync(ct);

		return projects.Select(AllocationMapper.ToDto).ToList();
	}

	public async Task<ProjectWithAllocationsDto> CreateProjectAsync(CreateProjectDto dto, CancellationToken ct = default)
	{
		await CreateProjectValidator.ValidateAndThrowAsync(dto, ct);

		var maxPos = await Db.Projects.Where(p => !p.IsArchived).Select(p => (int?)p.Position).MaxAsync(ct) ?? -1;

		var project = new Project
		{
			Id = Guid.CreateVersion7(),
			Title = dto.Title,
			Description = dto.Description,
			ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#2A6B7E" : dto.ColorHex!,
			Position = maxPos + 1,
		};
		Db.Projects.Add(project);
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(project);
	}

	public async Task<ProjectWithAllocationsDto> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto, CancellationToken ct = default)
	{
		await UpdateProjectValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(p => p.Allocations).ThenInclude(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");

		project.Title = dto.Title;
		project.Description = dto.Description;
		project.ColorHex = dto.ColorHex;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(project);
	}

	public async Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
	{
		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
			?? throw new KeyNotFoundException($"Project {projectId} not found.");
		project.IsArchived = true;
		project.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}

	public async Task ReorderProjectsAsync(ReorderProjectsDto dto, CancellationToken ct = default)
	{
		var projects = await Db.Projects.Where(p => dto.OrderedIds.Contains(p.Id)).ToListAsync(ct);
		for (var i = 0; i < dto.OrderedIds.Count; i++)
		{
			var p = projects.FirstOrDefault(x => x.Id == dto.OrderedIds[i]);
			if (p != null) { p.Position = i; p.UpdatedAt = DateTime.UtcNow; }
		}
		await Db.SaveChangesAsync(ct);
	}

	public async Task<AllocationDto> AllocateAsync(CreateAllocationDto dto, Guid currentCollaboratorId, CancellationToken ct = default)
	{
		await CreateAllocationValidator.ValidateAndThrowAsync(dto, ct);

		var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
			?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
		if (project.IsArchived)
			throw new InvalidOperationException("Cannot allocate to an archived project.");

		var existing = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.ProjectId == dto.ProjectId && a.CollaboratorId == dto.CollaboratorId, ct);

		if (existing != null)
		{
			if (dto.Note != null) { existing.Note = dto.Note; existing.UpdatedAt = DateTime.UtcNow; await Db.SaveChangesAsync(ct); }
			return AllocationMapper.ToDto(existing);
		}

		var maxPos = await Db.ProjectAllocations.Where(a => a.ProjectId == dto.ProjectId)
			.Select(a => (int?)a.Position).MaxAsync(ct) ?? -1;

		var alloc = new ProjectAllocation
		{
			Id = Guid.CreateVersion7(),
			ProjectId = dto.ProjectId,
			CollaboratorId = dto.CollaboratorId,
			Note = dto.Note,
			Position = maxPos + 1,
			AllocatedAt = DateTime.UtcNow,
			AllocatedById = currentCollaboratorId,
		};
		Db.ProjectAllocations.Add(alloc);
		await Db.SaveChangesAsync(ct);

		await Db.Entry(alloc).Reference(a => a.Collaborator).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Role).LoadAsync(ct);
		await Db.Entry(alloc.Collaborator).Reference(c => c.Department).LoadAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task<AllocationDto> MoveAllocationAsync(Guid allocationId, MoveAllocationDto dto, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");

		var target = await Db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId, ct)
			?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
		if (target.IsArchived)
			throw new InvalidOperationException("Cannot move to an archived project.");

		// Merge: if the collaborator already has an allocation in the target project, keep that one and drop this.
		if (dto.ProjectId != alloc.ProjectId)
		{
			var clash = await Db.ProjectAllocations
				.Include(a => a.Collaborator).ThenInclude(c => c.Role)
				.Include(a => a.Collaborator).ThenInclude(c => c.Department)
				.FirstOrDefaultAsync(a => a.ProjectId == dto.ProjectId && a.CollaboratorId == alloc.CollaboratorId, ct);
			if (clash != null)
			{
				if (alloc.Note != null && clash.Note == null) clash.Note = alloc.Note;
				alloc.IsDeleted = true;
				alloc.DeletedAt = DateTime.UtcNow;
				clash.Position = dto.Position;
				clash.UpdatedAt = DateTime.UtcNow;
				await Db.SaveChangesAsync(ct);
				return AllocationMapper.ToDto(clash);
			}
		}

		alloc.ProjectId = dto.ProjectId;
		alloc.Position = dto.Position;
		alloc.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task<AllocationDto> UpdateNoteAsync(Guid allocationId, UpdateNoteDto dto, CancellationToken ct = default)
	{
		await UpdateNoteValidator.ValidateAndThrowAsync(dto, ct);
		var alloc = await Db.ProjectAllocations
			.Include(a => a.Collaborator).ThenInclude(c => c.Role)
			.Include(a => a.Collaborator).ThenInclude(c => c.Department)
			.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");
		alloc.Note = dto.Note;
		alloc.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return AllocationMapper.ToDto(alloc);
	}

	public async Task RemoveAllocationAsync(Guid allocationId, CancellationToken ct = default)
	{
		var alloc = await Db.ProjectAllocations.FirstOrDefaultAsync(a => a.Id == allocationId, ct)
			?? throw new KeyNotFoundException($"Allocation {allocationId} not found.");
		alloc.IsDeleted = true;
		alloc.DeletedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
	}
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Waao.Tests/Waao.Tests.csproj --filter "FullyQualifiedName~AllocationServiceTests"`
Expected: PASS (7 tests).

> If `Move_ToProjectWhereCollaboratorAlreadyExists_MergesNoDuplicate` fails because the soft-deleted row still appears: confirm the `ProjectAllocation` query filter `!IsDeleted` is applied (Task 2). InMemory honors query filters.

- [ ] **Step 5: Commit**

```bash
git add src/Waao.Services/Services/Allocation/AllocationService.cs tests/Waao.Tests/Allocation/AllocationServiceTests.cs
git commit -m "feat(allocation): implement AllocationService with tests"
```

---

## Task 9: Controller + DI registration

**Files:**
- Create: `src/Waao.API/Controllers/AllocationsController.cs`
- Modify: `src/Waao.API/Program.cs` (after the `IKanbanService` registration, ~line 92)

- [ ] **Step 1: Write the controller** (mirrors `KanbanController`; admin endpoints carry `[Authorize(Policy = "Admin")]`)

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Waao.Services.Abstractions.Dtos.Allocation;
using Waao.Services.Abstractions.Services;

namespace Waao.API.Controllers;

[ApiController]
[Route("api/waao/allocations")]
[Authorize]
public class AllocationsController(IAllocationService Service) : ControllerBase
{
	private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
		? id : throw new UnauthorizedAccessException("Missing subject claim.");

	[HttpGet("board")]
	[ProducesResponseType(typeof(AllocationBoardDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetBoard(CancellationToken ct)
		=> Ok(await Service.GetBoardAsync(ct));

	[HttpGet("by-collaborator/{id:guid}")]
	[ProducesResponseType(typeof(IReadOnlyList<ProjectWithAllocationsDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByCollaborator(Guid id, CancellationToken ct)
		=> Ok(await Service.GetByCollaboratorAsync(id, ct));

	// ----- Project config (admin) -----
	[HttpPost("projects")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(ProjectWithAllocationsDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.CreateProjectAsync(dto, ct));

	[HttpPut("projects/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(typeof(ProjectWithAllocationsDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateProjectAsync(id, dto, ct));

	[HttpPut("projects/reorder")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ReorderProjects([FromBody] ReorderProjectsDto dto, CancellationToken ct)
	{
		await Service.ReorderProjectsAsync(dto, ct);
		return NoContent();
	}

	[HttpDelete("projects/{id:guid}")]
	[Authorize(Policy = "Admin")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> ArchiveProject(Guid id, CancellationToken ct)
	{
		await Service.ArchiveProjectAsync(id, ct);
		return NoContent();
	}

	// ----- Allocations (any collaborator) -----
	[HttpPost]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> Allocate([FromBody] CreateAllocationDto dto, CancellationToken ct)
		=> Created(string.Empty, await Service.AllocateAsync(dto, Me, ct));

	[HttpPut("{id:guid}/move")]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Move(Guid id, [FromBody] MoveAllocationDto dto, CancellationToken ct)
		=> Ok(await Service.MoveAllocationAsync(id, dto, ct));

	[HttpPut("{id:guid}/note")]
	[ProducesResponseType(typeof(AllocationDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteDto dto, CancellationToken ct)
		=> Ok(await Service.UpdateNoteAsync(id, dto, ct));

	[HttpDelete("{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
	{
		await Service.RemoveAllocationAsync(id, ct);
		return NoContent();
	}
}
```

- [ ] **Step 2: Register the service in `Program.cs`**

After `builder.Services.AddScoped<IKanbanService, ...>();` (~line 92) add:

```csharp
builder.Services.AddScoped<IAllocationService, Waao.Services.Services.Allocation.AllocationService>();
```

> Validators: confirm how validators are registered. Run `grep -n "AddValidatorsFromAssembly\|AddScoped<IValidator" src/Waao.API/Program.cs`. If `AddValidatorsFromAssembly` is used, the four new validators are auto-discovered — no change. If validators are registered individually, add the four explicitly.

- [ ] **Step 3: Build the API project**

Run: `dotnet build src/Waao.API/Waao.API.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Run the full backend test suite**

Run: `dotnet test tests/Waao.Tests/Waao.Tests.csproj`
Expected: PASS (existing + 7 new).

- [ ] **Step 5: Commit**

```bash
git add src/Waao.API/Controllers/AllocationsController.cs src/Waao.API/Program.cs
git commit -m "feat(allocation): add AllocationsController and DI registration"
```

---

## Task 10: Frontend types

**Files:**
- Create: `src/types/allocations.types.ts`

- [ ] **Step 1: Write the types** (no `any`)

```typescript
export interface CollaboratorChip {
	id: string;
	fullName: string;
	photoUrl?: string | null;
	roleTitle?: string | null;
	departmentName?: string | null;
}

export interface Allocation {
	id: string;
	projectId: string;
	note?: string | null;
	position: number;
	allocatedAt: string;
	collaborator: CollaboratorChip;
}

export interface ProjectWithAllocations {
	id: string;
	title: string;
	description?: string | null;
	colorHex: string;
	position: number;
	allocations: Allocation[];
}

export interface AllocationBoard {
	projects: ProjectWithAllocations[];
	collaborators: CollaboratorChip[];
}

export interface CreateProjectDto {
	title: string;
	description?: string;
	colorHex?: string;
}

export interface UpdateProjectDto {
	title: string;
	description?: string;
	colorHex: string;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/types/allocations.types.ts
git commit -m "feat(allocation): frontend types"
```

---

## Task 11: Frontend service

**Files:**
- Create: `src/services/allocations.service.ts`

- [ ] **Step 1: Write the service** (object literal, mirrors `kanban.service.ts`; baseURL already `/api/waao`)

```typescript
import { apiClient } from '@/lib/api-client';
import type {
	AllocationBoard, Allocation, ProjectWithAllocations,
	CreateProjectDto, UpdateProjectDto,
} from '@/types/allocations.types';

export const allocationsService = {
	getBoard: async (): Promise<AllocationBoard> =>
		(await apiClient.get<AllocationBoard>('/allocations/board')).data,

	getByCollaborator: async (collaboratorId: string): Promise<ProjectWithAllocations[]> =>
		(await apiClient.get<ProjectWithAllocations[]>(`/allocations/by-collaborator/${collaboratorId}`)).data,

	// projects (admin)
	createProject: async (dto: CreateProjectDto): Promise<ProjectWithAllocations> =>
		(await apiClient.post<ProjectWithAllocations>('/allocations/projects', dto)).data,
	updateProject: async (id: string, dto: UpdateProjectDto): Promise<ProjectWithAllocations> =>
		(await apiClient.put<ProjectWithAllocations>(`/allocations/projects/${id}`, dto)).data,
	reorderProjects: async (orderedIds: string[]): Promise<void> => {
		await apiClient.put('/allocations/projects/reorder', { orderedIds });
	},
	archiveProject: async (id: string): Promise<void> => {
		await apiClient.delete(`/allocations/projects/${id}`);
	},

	// allocations (any)
	allocate: async (projectId: string, collaboratorId: string, note?: string): Promise<Allocation> =>
		(await apiClient.post<Allocation>('/allocations', { projectId, collaboratorId, note })).data,
	move: async (id: string, projectId: string, position: number): Promise<Allocation> =>
		(await apiClient.put<Allocation>(`/allocations/${id}/move`, { projectId, position })).data,
	updateNote: async (id: string, note: string): Promise<Allocation> =>
		(await apiClient.put<Allocation>(`/allocations/${id}/note`, { note })).data,
	remove: async (id: string): Promise<void> => {
		await apiClient.delete(`/allocations/${id}`);
	},
};
```

- [ ] **Step 2: Commit**

```bash
git add src/services/allocations.service.ts
git commit -m "feat(allocation): frontend service"
```

---

## Task 12: Project edit dialog (admin)

**Files:**
- Create: `src/pages/allocations/project-edit-dialog.tsx`

> Reuse the same UI primitives the kanban `board-edit-dialog.tsx` uses. Before writing, run `sed -n '1,40p' src/pages/kanban/board-edit-dialog.tsx` to confirm the Dialog import path and prop shape, and match it.

- [ ] **Step 1: Write the dialog**

```tsx
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { allocationsService } from '@/services/allocations.service';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import type { ProjectWithAllocations } from '@/types/allocations.types';

const PRESET_COLORS = ['#2A6B7E', '#7E2A5B', '#2A7E45', '#7E5B2A', '#452A7E', '#7E2A2A'];

interface Props {
	open: boolean;
	onOpenChange: (open: boolean) => void;
	project?: ProjectWithAllocations | null;
}

export function ProjectEditDialog({ open, onOpenChange, project }: Props) {
	const { t } = useTranslation();
	const qc = useQueryClient();
	const editing = !!project;

	const [title, setTitle] = useState(project?.title ?? '');
	const [description, setDescription] = useState(project?.description ?? '');
	const [colorHex, setColorHex] = useState(project?.colorHex ?? PRESET_COLORS[0]);

	const save = useMutation({
		mutationFn: () =>
			editing
				? allocationsService.updateProject(project!.id, { title, description: description || undefined, colorHex })
				: allocationsService.createProject({ title, description: description || undefined, colorHex }),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ['allocation-board'] });
			onOpenChange(false);
		},
	});

	const archive = useMutation({
		mutationFn: () => allocationsService.archiveProject(project!.id),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ['allocation-board'] });
			onOpenChange(false);
		},
	});

	return (
		<Dialog open={open} onOpenChange={onOpenChange}>
			<DialogContent>
				<DialogHeader>
					<DialogTitle>{editing ? t('allocations.editProject') : t('allocations.newProject')}</DialogTitle>
				</DialogHeader>

				<div className="space-y-4">
					<Input
						value={title}
						onChange={e => setTitle(e.target.value)}
						placeholder={t('allocations.projectTitlePlaceholder')}
						aria-label={t('allocations.projectTitle')}
					/>
					<Textarea
						value={description}
						onChange={e => setDescription(e.target.value)}
						placeholder={t('allocations.projectDescriptionPlaceholder')}
						aria-label={t('allocations.projectDescription')}
					/>
					<div className="flex gap-2" role="radiogroup" aria-label={t('allocations.projectColor')}>
						{PRESET_COLORS.map(c => (
							<button
								key={c}
								type="button"
								role="radio"
								aria-checked={colorHex === c}
								onClick={() => setColorHex(c)}
								className="h-7 w-7 rounded-full border-2"
								style={{ backgroundColor: c, borderColor: colorHex === c ? '#fff' : 'transparent' }}
							/>
						))}
					</div>
				</div>

				<DialogFooter className="flex justify-between">
					{editing && (
						<Button variant="ghost" onClick={() => archive.mutate()} disabled={archive.isPending}>
							{t('allocations.archive')}
						</Button>
					)}
					<Button onClick={() => save.mutate()} disabled={!title.trim() || save.isPending}>
						{t('common.save')}
					</Button>
				</DialogFooter>
			</DialogContent>
		</Dialog>
	);
}
```

> If `@/components/ui/textarea` does not exist, run `ls src/components/ui | grep -i textarea`; if absent, use a multiline `Input` or the component the kanban card-drawer uses for descriptions (check `card-drawer.tsx`).

- [ ] **Step 2: Type-check**

Run: `npx tsc --noEmit`
Expected: no errors in `project-edit-dialog.tsx` (i18n keys added in Task 14; that's fine — `t()` is untyped string).

- [ ] **Step 3: Commit**

```bash
git add src/pages/allocations/project-edit-dialog.tsx
git commit -m "feat(allocation): project edit dialog"
```

---

## Task 13: Allocation board page (dnd)

**Files:**
- Create: `src/pages/allocations/allocation-board-page.tsx`

> This reuses the `@dnd-kit` setup from `board-view.tsx` (already read). Pool draggables use id `pool:<collaboratorId>`; existing allocation draggables use id `alloc:<allocationId>`. Boxes are droppables with id `box:<projectId>`. Dropping a `pool:` item on a box = allocate; dropping an `alloc:` item on a box = move.

- [ ] **Step 1: Write the page**

```tsx
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
	DndContext, DragOverlay, PointerSensor, useSensor, useSensors,
	pointerWithin, useDroppable, useDraggable,
	type DragEndEvent, type DragStartEvent,
} from '@dnd-kit/core';
import { Plus, X, Settings2, Search } from 'lucide-react';
import { allocationsService } from '@/services/allocations.service';
import { useAuth } from '@/hooks/use-auth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { ProjectEditDialog } from './project-edit-dialog';
import { cn } from '@/lib/utils';
import type { AllocationBoard, Allocation, CollaboratorChip, ProjectWithAllocations } from '@/types/allocations.types';

const POOL = 'pool:';
const ALLOC = 'alloc:';
const BOX = 'box:';

function initials(name: string) {
	return name.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
}

export function AllocationBoardPage() {
	const { t } = useTranslation();
	const qc = useQueryClient();
	const { me } = useAuth();
	const isStaff = me?.roleKind === 'Admin' || me?.roleKind === 'HR';

	const { data, isLoading } = useQuery({ queryKey: ['allocation-board'], queryFn: () => allocationsService.getBoard() });

	const [search, setSearch] = useState('');
	const [activeChip, setActiveChip] = useState<CollaboratorChip | null>(null);
	const [dialogOpen, setDialogOpen] = useState(false);
	const [editProject, setEditProject] = useState<ProjectWithAllocations | null>(null);

	const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

	const allocate = useMutation({
		mutationFn: (a: { projectId: string; collaboratorId: string }) => allocationsService.allocate(a.projectId, a.collaboratorId),
		onSuccess: () => qc.invalidateQueries({ queryKey: ['allocation-board'] }),
	});
	const move = useMutation({
		mutationFn: (a: { id: string; projectId: string; position: number }) => allocationsService.move(a.id, a.projectId, a.position),
		onSuccess: () => qc.invalidateQueries({ queryKey: ['allocation-board'] }),
	});
	const remove = useMutation({
		mutationFn: (id: string) => allocationsService.remove(id),
		onSuccess: () => qc.invalidateQueries({ queryKey: ['allocation-board'] }),
	});
	const saveNote = useMutation({
		mutationFn: (a: { id: string; note: string }) => allocationsService.updateNote(a.id, a.note),
		onSuccess: () => qc.invalidateQueries({ queryKey: ['allocation-board'] }),
	});

	const board: AllocationBoard = data ?? { projects: [], collaborators: [] };

	const filteredPool = useMemo(
		() => board.collaborators.filter(c => c.fullName.toLowerCase().includes(search.toLowerCase())),
		[board.collaborators, search],
	);
	const allocatedIds = useMemo(
		() => new Set(board.projects.flatMap(p => p.allocations.map(a => a.collaborator.id))),
		[board.projects],
	);

	function onDragStart(e: DragStartEvent) {
		const id = String(e.active.id);
		if (id.startsWith(POOL)) {
			const cid = id.slice(POOL.length);
			setActiveChip(board.collaborators.find(c => c.id === cid) ?? null);
		} else if (id.startsWith(ALLOC)) {
			const aid = id.slice(ALLOC.length);
			const a = board.projects.flatMap(p => p.allocations).find(x => x.id === aid);
			setActiveChip(a?.collaborator ?? null);
		}
	}

	function onDragEnd(e: DragEndEvent) {
		setActiveChip(null);
		const overId = e.over ? String(e.over.id) : null;
		if (!overId || !overId.startsWith(BOX)) return;
		const projectId = overId.slice(BOX.length);
		const activeId = String(e.active.id);
		const targetCount = board.projects.find(p => p.id === projectId)?.allocations.length ?? 0;

		if (activeId.startsWith(POOL)) {
			allocate.mutate({ projectId, collaboratorId: activeId.slice(POOL.length) });
		} else if (activeId.startsWith(ALLOC)) {
			move.mutate({ id: activeId.slice(ALLOC.length), projectId, position: targetCount });
		}
	}

	if (isLoading) return <p className="text-muted-foreground text-sm p-6">{t('common.loading')}</p>;

	return (
		<DndContext sensors={sensors} collisionDetection={pointerWithin} onDragStart={onDragStart} onDragEnd={onDragEnd}>
			<div className="flex h-full gap-4 p-4">
				{/* Pool */}
				<aside className="w-64 shrink-0 flex flex-col gap-2">
					<h2 className="text-sm font-semibold">{t('allocations.people')}</h2>
					<div className="relative">
						<Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
						<Input className="pl-8" value={search} onChange={e => setSearch(e.target.value)} placeholder={t('allocations.searchPeople')} />
					</div>
					<div className="flex flex-col gap-1 overflow-y-auto">
						{filteredPool.map(c => (
							<PoolChip key={c.id} collaborator={c} unallocated={!allocatedIds.has(c.id)} unallocatedLabel={t('allocations.unallocated')} />
						))}
					</div>
				</aside>

				{/* Boxes */}
				<div className="flex-1 overflow-auto">
					<div className="flex items-center justify-between mb-3">
						<h1 className="text-lg font-semibold">{t('allocations.title')}</h1>
						{isStaff && (
							<Button size="sm" onClick={() => { setEditProject(null); setDialogOpen(true); }}>
								<Plus className="h-4 w-4 mr-1" />{t('allocations.newProject')}
							</Button>
						)}
					</div>
					<div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
						{board.projects.map(p => (
							<ProjectBox
								key={p.id}
								project={p}
								isStaff={isStaff}
								onEdit={() => { setEditProject(p); setDialogOpen(true); }}
								onRemove={id => remove.mutate(id)}
								onSaveNote={(id, note) => saveNote.mutate({ id, note })}
								tEmpty={t('allocations.dropHere')}
							/>
						))}
						{board.projects.length === 0 && (
							<p className="text-muted-foreground text-sm">{t('allocations.noProjects')}</p>
						)}
					</div>
				</div>
			</div>

			<DragOverlay>
				{activeChip && (
					<div className="flex items-center gap-2 rounded-md bg-card border px-2 py-1 shadow-lg">
						<Avatar className="h-6 w-6">
							{activeChip.photoUrl && <AvatarImage src={activeChip.photoUrl} alt={activeChip.fullName} />}
							<AvatarFallback>{initials(activeChip.fullName)}</AvatarFallback>
						</Avatar>
						<span className="text-sm">{activeChip.fullName}</span>
					</div>
				)}
			</DragOverlay>

			<ProjectEditDialog open={dialogOpen} onOpenChange={setDialogOpen} project={editProject} />
		</DndContext>
	);
}

function PoolChip({ collaborator, unallocated, unallocatedLabel }: { collaborator: CollaboratorChip; unallocated: boolean; unallocatedLabel: string }) {
	const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: `${POOL}${collaborator.id}` });
	return (
		<div
			ref={setNodeRef}
			{...listeners}
			{...attributes}
			className={cn('flex items-center gap-2 rounded-md border px-2 py-1 cursor-grab bg-card', isDragging && 'opacity-40')}
		>
			<Avatar className="h-6 w-6">
				{collaborator.photoUrl && <AvatarImage src={collaborator.photoUrl} alt={collaborator.fullName} />}
				<AvatarFallback>{initials(collaborator.fullName)}</AvatarFallback>
			</Avatar>
			<span className="text-sm truncate flex-1">{collaborator.fullName}</span>
			{unallocated && <span className="h-2 w-2 rounded-full bg-amber-500" title={unallocatedLabel} />}
		</div>
	);
}

function ProjectBox({
	project, isStaff, onEdit, onRemove, onSaveNote, tEmpty,
}: {
	project: ProjectWithAllocations;
	isStaff: boolean;
	onEdit: () => void;
	onRemove: (id: string) => void;
	onSaveNote: (id: string, note: string) => void;
	tEmpty: string;
}) {
	const { setNodeRef, isOver } = useDroppable({ id: `${BOX}${project.id}` });
	return (
		<div ref={setNodeRef} className={cn('rounded-lg border bg-card flex flex-col', isOver && 'ring-2 ring-primary')}>
			<div className="h-1 rounded-t-lg" style={{ backgroundColor: project.colorHex }} />
			<div className="flex items-center justify-between px-3 py-2">
				<div className="flex items-center gap-2">
					<span className="font-medium text-sm">{project.title}</span>
					<span className="text-xs text-muted-foreground">{project.allocations.length}</span>
				</div>
				{isStaff && (
					<button onClick={onEdit} aria-label="edit" className="text-muted-foreground hover:text-foreground">
						<Settings2 className="h-4 w-4" />
					</button>
				)}
			</div>
			<div className="flex flex-col gap-1 p-2 min-h-[80px]">
				{project.allocations.map(a => (
					<AllocationChip key={a.id} allocation={a} onRemove={onRemove} onSaveNote={onSaveNote} />
				))}
				{project.allocations.length === 0 && (
					<p className="text-xs text-muted-foreground border border-dashed rounded-md p-3 text-center">{tEmpty}</p>
				)}
			</div>
		</div>
	);
}

function AllocationChip({
	allocation, onRemove, onSaveNote,
}: {
	allocation: Allocation;
	onRemove: (id: string) => void;
	onSaveNote: (id: string, note: string) => void;
}) {
	const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: `${ALLOC}${allocation.id}` });
	const [editing, setEditing] = useState(false);
	const [note, setNote] = useState(allocation.note ?? '');

	return (
		<div ref={setNodeRef} className={cn('rounded-md border bg-background px-2 py-1', isDragging && 'opacity-40')}>
			<div className="flex items-center gap-2">
				<span {...listeners} {...attributes} className="flex items-center gap-2 flex-1 cursor-grab min-w-0">
					<Avatar className="h-6 w-6">
						{allocation.collaborator.photoUrl && <AvatarImage src={allocation.collaborator.photoUrl} alt={allocation.collaborator.fullName} />}
						<AvatarFallback>{initials(allocation.collaborator.fullName)}</AvatarFallback>
					</Avatar>
					<span className="text-sm truncate">{allocation.collaborator.fullName}</span>
				</span>
				<button onClick={() => onRemove(allocation.id)} aria-label="remove" className="text-muted-foreground hover:text-destructive">
					<X className="h-3.5 w-3.5" />
				</button>
			</div>
			{editing ? (
				<Input
					autoFocus
					value={note}
					onChange={e => setNote(e.target.value)}
					onBlur={() => { setEditing(false); if (note !== (allocation.note ?? '')) onSaveNote(allocation.id, note); }}
					onKeyDown={e => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
					className="mt-1 h-6 text-xs"
				/>
			) : (
				<button onClick={() => setEditing(true)} className="mt-0.5 text-xs italic text-muted-foreground hover:text-foreground text-left w-full truncate">
					{allocation.note || '+ note'}
				</button>
			)}
		</div>
	);
}
```

> Confirm `@/components/ui/avatar` exports `Avatar/AvatarImage/AvatarFallback` (the sidebar uses them — it does). Confirm `useAuth` returns `me.roleKind` (sidebar uses exactly `me?.roleKind === 'Admin'`).

- [ ] **Step 2: Type-check**

Run: `npx tsc --noEmit`
Expected: no type errors.

- [ ] **Step 3: Commit**

```bash
git add src/pages/allocations/allocation-board-page.tsx
git commit -m "feat(allocation): allocation board page with drag-and-drop"
```

---

## Task 14: Route, sidebar, and i18n

**Files:**
- Modify: `src/App.tsx` (add import + route near the `/boards` route ~line 125)
- Modify: `src/components/layout/sidebar.tsx` (nav item in the "work" section ~line 119, + icon import)
- Modify: `src/locales/pt-BR/common.json`, `src/locales/en/common.json`, `src/locales/es/common.json`

- [ ] **Step 1: Add the route in `App.tsx`**

Add the lazy/static import alongside the other page imports (match how `BoardsPage` is imported — check top of `App.tsx`):

```tsx
import { AllocationBoardPage } from '@/pages/allocations/allocation-board-page';
```

Add inside the inner `<Routes>` (after the `/boards/:slug` route, ~line 126):

```tsx
						<Route path="/allocations" element={<AllocationBoardPage />} />
```

- [ ] **Step 2: Add the sidebar nav item**

In `src/components/layout/sidebar.tsx`, add `Target` to the lucide import line (line 5):

```tsx
	LayoutDashboard, Users, CalendarDays, SquareKanban, MessagesSquare, Lightbulb, Target,
```

Add the nav item in the "work" `NavSection` after the boards item (~line 119):

```tsx
					<NavItem to="/allocations" icon={Target} label={t('sidebar.navItem.allocations')} />
```

- [ ] **Step 3: Add i18n keys — pt-BR** (`src/locales/pt-BR/common.json`)

Add a top-level `"allocations"` object and the `sidebar.navItem.allocations` key. Match the existing nesting style of the file:

```json
"allocations": {
	"title": "Quem está em quê",
	"people": "Pessoas",
	"searchPeople": "Buscar pessoas...",
	"unallocated": "Sem alocação",
	"newProject": "Novo projeto",
	"editProject": "Editar projeto",
	"archive": "Arquivar",
	"noProjects": "Nenhum projeto ainda. Crie o primeiro.",
	"dropHere": "Arraste pessoas para cá",
	"projectTitle": "Título do projeto",
	"projectTitlePlaceholder": "Ex.: Faturamento",
	"projectDescription": "Descrição",
	"projectDescriptionPlaceholder": "Opcional",
	"projectColor": "Cor"
}
```

And add to the existing `sidebar.navItem` object:

```json
"allocations": "Alocação"
```

- [ ] **Step 4: Add i18n keys — en** (`src/locales/en/common.json`)

```json
"allocations": {
	"title": "Who's working on what",
	"people": "People",
	"searchPeople": "Search people...",
	"unallocated": "Unallocated",
	"newProject": "New project",
	"editProject": "Edit project",
	"archive": "Archive",
	"noProjects": "No projects yet. Create the first one.",
	"dropHere": "Drag people here",
	"projectTitle": "Project title",
	"projectTitlePlaceholder": "e.g. Billing",
	"projectDescription": "Description",
	"projectDescriptionPlaceholder": "Optional",
	"projectColor": "Color"
}
```

`sidebar.navItem.allocations`: `"Allocation"`

- [ ] **Step 5: Add i18n keys — es** (`src/locales/es/common.json`)

```json
"allocations": {
	"title": "Quién trabaja en qué",
	"people": "Personas",
	"searchPeople": "Buscar personas...",
	"unallocated": "Sin asignar",
	"newProject": "Nuevo proyecto",
	"editProject": "Editar proyecto",
	"archive": "Archivar",
	"noProjects": "Aún no hay proyectos. Crea el primero.",
	"dropHere": "Arrastra personas aquí",
	"projectTitle": "Título del proyecto",
	"projectTitlePlaceholder": "Ej.: Facturación",
	"projectDescription": "Descripción",
	"projectDescriptionPlaceholder": "Opcional",
	"projectColor": "Color"
}
```

`sidebar.navItem.allocations`: `"Asignación"`

- [ ] **Step 6: Validate JSON + type-check + build**

Run:
```bash
node -e "['pt-BR','en','es'].forEach(l=>JSON.parse(require('fs').readFileSync('src/locales/'+l+'/common.json','utf8')))" && echo "JSON OK"
npx tsc --noEmit
npm run build
```
Expected: "JSON OK", no type errors, build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/App.tsx src/components/layout/sidebar.tsx src/locales/
git commit -m "feat(allocation): route, sidebar nav, and i18n (pt-BR/en/es)"
```

---

## Task 15: Manual verification

- [ ] **Step 1: Run backend + frontend locally**

Backend: `dotnet run --project src/Waao.API` (from `WaaoBackend/`).
Frontend: `npm run dev` (from `WaaoFrontend/`).

- [ ] **Step 2: Smoke test**

1. Sign in as an Admin. Navigate to `/allocations` (sidebar → "Alocação").
2. Create a project box. Confirm it appears.
3. Drag a person from the pool into the box → chip appears; pool dot for that person clears.
4. Click the chip note → type a note → blur → note persists after refresh.
5. Drag the chip to another box → moves.
6. Drag the same person from the pool into a second box → appears in both.
7. Click X on a chip → removed.
8. Sign in as a non-admin → no "New project" button / no gear, but drag still works.

- [ ] **Step 3: No commit** (verification only).

---

## Deployment notes (from project memory — do at user's request only)

- waao-api **must stay at 1 Fly machine** (SignalR has no Redis backplane).
- After `git push`, the frontend **must** be force-deployed: `npm run deploy` (Cloudflare Worker auto-deploy does not work). Backend auto-deploys on push to Fly.
- Do not push/deploy unless the user explicitly asks.

---

## Self-review notes

- **Spec coverage:** Project entity (T1–3), multiple allocations (unique-per-project index + pool always draggable, T2/T13), free-text note (T1/T8/T13), admin-config-anyone-moves (policy on controller T9 + `isStaff` UI gate T13), grid layout (T13), archive hides (T8), idempotent allocate + merge-on-move (T8). All covered.
- **Out of scope confirmed:** no history UI, no SignalR — matches spec.
- **Type consistency:** `allocationsService` method names match service calls in T13; DTO field names (`colorHex`, `orderedIds`, `note`, `position`) consistent backend↔frontend; query key `['allocation-board']` used uniformly.
- **Assumptions flagged inline:** `Role.Title`/`Department.Name` (T7 step 2 verifies), validator registration style (T9 step 2 verifies), `Textarea`/`Dialog` component existence (T12 notes), `BoardsPage` import style (T14).
