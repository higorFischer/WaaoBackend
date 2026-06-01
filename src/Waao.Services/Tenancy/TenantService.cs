using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Waao.Infra.EF;
using Waao.Services.Abstractions.Dtos;
using Waao.Services.Abstractions.Services;
using Waao.Services.Auth;

namespace Waao.Services.Tenancy;

public sealed class TenantService(
	WaaoDbContext Db,
	ITenantContext TenantContext,
	JwtIssuer Jwt,
	ILogger<TenantService> Logger) : ITenantService
{
	public async Task<IReadOnlyList<TenantDto>> ListForEmailAsync(string email, CancellationToken ct = default)
	{
		var emailLower = email.Trim().ToLowerInvariant();
		// This endpoint is anonymous (login picker calls it before any JWT exists),
		// so the global tenant filter would scope it to WAAO and Liberty would
		// never appear. Ignore filters here — it's the whole point of the picker.
		var collaborators = await Db.Collaborators.AsNoTracking()
			.IgnoreQueryFilters()
			.Where(c => !c.IsDeleted && c.Email == emailLower && c.TenantId != null)
			.Select(c => c.TenantId!.Value)
			.Distinct()
			.ToListAsync(ct);

		if (collaborators.Count == 0) return [];

		var tenants = await Db.Tenants.AsNoTracking()
			.Where(t => collaborators.Contains(t.Id) && t.IsActive)
			.OrderBy(t => t.Name)
			.ToListAsync(ct);

		return tenants.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<TenantDto>> ListAllAsync(CancellationToken ct = default)
	{
		var rows = await Db.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
		return rows.Select(ToDto).ToList();
	}

	public async Task<TenantDto?> GetCurrentAsync(CancellationToken ct = default)
	{
		var id = TenantContext.CurrentTenantId;
		if (id is null) return null;
		var tenant = await Db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
		return tenant is null ? null : ToDto(tenant);
	}

	public async Task<AuthResultDto> SwitchAsync(Guid currentCollaboratorId, Guid targetTenantId, CancellationToken ct = default)
	{
		var current = await Db.Collaborators.AsNoTracking().FirstOrDefaultAsync(c => c.Id == currentCollaboratorId, ct)
			?? throw new UnauthorizedAccessException("Unknown collaborator.");

		// The sister account lives in the TARGET tenant — by definition NOT the
		// current tenant — so the global filter (which only allows the caller's
		// tenant) would always return null here. Bypass filters and constrain by
		// email + targetTenantId manually.
		var sister = await Db.Collaborators
			.IgnoreQueryFilters()
			.Where(c => !c.IsDeleted)
			.Include(c => c.Department).Include(c => c.Role).Include(c => c.Manager).Include(c => c.Badges)
			.FirstOrDefaultAsync(c => c.Email == current.Email && c.TenantId == targetTenantId, ct)
			?? throw new UnauthorizedAccessException("You don't have an account in that tenant.");

		var (token, expires) = Jwt.Issue(sister);
		Logger.LogInformation("Tenant switch {Email}: {From} -> {To}.", current.Email, current.TenantId, targetTenantId);

		return new AuthResultDto
		{
			Token = token,
			ExpiresAt = expires,
			Me = MapCollaborator(sister),
		};
	}

	public async Task<TenantDto> CreateAsync(Guid creatorCollaboratorId, CreateTenantDto dto, CancellationToken ct = default)
	{
		var name = (dto.Name ?? string.Empty).Trim();
		var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
		var accent = string.IsNullOrWhiteSpace(dto.AccentColorHex) ? "#2A6B7E" : dto.AccentColorHex.Trim();
		var logo = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim();

		if (name.Length == 0) throw new ArgumentException("Tenant name is required.", nameof(dto));
		if (slug.Length == 0) throw new ArgumentException("Tenant slug is required.", nameof(dto));
		if (slug.Length > 40) throw new ArgumentException("Slug too long (max 40 chars).", nameof(dto));
		if (!System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9-]+$"))
			throw new ArgumentException("Slug must be lowercase letters, digits, and hyphens only.", nameof(dto));

		var slugTaken = await Db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == slug, ct);
		if (slugTaken) throw new InvalidOperationException($"Tenant slug '{slug}' is already in use.");

		// Resolve the calling admin across tenants — they're calling from their own
		// tenant scope but we need to clone them into the new one.
		var creator = await Db.Collaborators
			.IgnoreQueryFilters()
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == creatorCollaboratorId && !c.IsDeleted, ct)
			?? throw new UnauthorizedAccessException("Unknown collaborator.");

		var tenant = new Domain.Models.Entities.Tenant
		{
			Id = Guid.CreateVersion7(),
			Name = name,
			Slug = slug,
			AccentColorHex = accent,
			LogoUrl = logo,
			IsActive = true,
		};
		Db.Tenants.Add(tenant);

		// Mirror the creator into the new tenant as its first admin so they can
		// switch in immediately. Same email → the workspace picker will surface
		// the new tenant on their next login too.
		var mirror = new Domain.Models.Entities.Collaborator
		{
			Id = Guid.CreateVersion7(),
			TenantId = tenant.Id,
			FullName = creator.FullName,
			Email = creator.Email,
			JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
			RoleKind = Domain.Models.Enums.CollaboratorRoleKind.Admin,
			PasswordHash = creator.PasswordHash,
			EmailVerified = true,
			EmailVerifiedAt = DateTime.UtcNow,
			Status = Domain.Models.Enums.CollaboratorStatus.Active,
			PhotoUrl = creator.PhotoUrl,
		};
		Db.Collaborators.Add(mirror);

		await Db.SaveChangesAsync(ct);
		Logger.LogInformation("Tenant created {Slug} ({Id}) by {Creator}; mirror collaborator {MirrorId}.", slug, tenant.Id, creator.Email, mirror.Id);
		return ToDto(tenant);
	}

	public async Task<AuthResultDto> JoinAsync(Guid currentCollaboratorId, Guid targetTenantId, CancellationToken ct = default)
	{
		var current = await Db.Collaborators
			.IgnoreQueryFilters()
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == currentCollaboratorId && !c.IsDeleted, ct)
			?? throw new UnauthorizedAccessException("Unknown collaborator.");

		var tenant = await Db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == targetTenantId, ct)
			?? throw new KeyNotFoundException($"Tenant {targetTenantId} not found.");

		// If a mirror already exists, just return a fresh JWT for it — idempotent join.
		var existing = await Db.Collaborators
			.IgnoreQueryFilters()
			.Include(c => c.Department).Include(c => c.Role).Include(c => c.Manager).Include(c => c.Badges)
			.FirstOrDefaultAsync(c => c.Email == current.Email && c.TenantId == targetTenantId && !c.IsDeleted, ct);

		Domain.Models.Entities.Collaborator mirror;
		if (existing is not null)
		{
			mirror = existing;
		}
		else
		{
			mirror = new Domain.Models.Entities.Collaborator
			{
				Id = Guid.CreateVersion7(),
				TenantId = targetTenantId,
				FullName = current.FullName,
				Email = current.Email,
				JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
				RoleKind = Domain.Models.Enums.CollaboratorRoleKind.Admin,
				PasswordHash = current.PasswordHash,
				EmailVerified = true,
				EmailVerifiedAt = DateTime.UtcNow,
				Status = Domain.Models.Enums.CollaboratorStatus.Active,
				PhotoUrl = current.PhotoUrl,
			};
			Db.Collaborators.Add(mirror);
			await Db.SaveChangesAsync(ct);
			Logger.LogInformation("Admin {Email} joined tenant {TenantId} ({Slug}); mirror {MirrorId}.", current.Email, targetTenantId, tenant.Slug, mirror.Id);
		}

		var (token, expires) = Jwt.Issue(mirror);
		return new AuthResultDto
		{
			Token = token,
			ExpiresAt = expires,
			Me = MapCollaborator(mirror),
		};
	}

	public async Task<TenantDto> UpdateAsync(Guid tenantId, UpdateTenantDto dto, CancellationToken ct = default)
	{
		var name = (dto.Name ?? string.Empty).Trim();
		var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
		var accent = string.IsNullOrWhiteSpace(dto.AccentColorHex) ? "#2A6B7E" : dto.AccentColorHex.Trim();
		var logo = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim();

		if (name.Length == 0) throw new ArgumentException("Tenant name is required.", nameof(dto));
		if (slug.Length == 0) throw new ArgumentException("Tenant slug is required.", nameof(dto));
		if (!System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9-]+$"))
			throw new ArgumentException("Slug must be lowercase letters, digits, and hyphens only.", nameof(dto));

		var tenant = await Db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
			?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

		if (!string.Equals(tenant.Slug, slug, StringComparison.Ordinal))
		{
			var slugTaken = await Db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == slug && t.Id != tenantId, ct);
			if (slugTaken) throw new InvalidOperationException($"Tenant slug '{slug}' is already in use.");
		}

		tenant.Name = name;
		tenant.Slug = slug;
		tenant.AccentColorHex = accent;
		tenant.LogoUrl = logo;
		tenant.IsActive = dto.IsActive;
		tenant.UpdatedAt = DateTime.UtcNow;
		await Db.SaveChangesAsync(ct);
		return ToDto(tenant);
	}

	private static TenantDto ToDto(Domain.Models.Entities.Tenant t) => new()
	{
		Id = t.Id,
		Name = t.Name,
		Slug = t.Slug,
		LogoUrl = t.LogoUrl,
		AccentColorHex = t.AccentColorHex,
		IsActive = t.IsActive,
	};

	// Light mapper to avoid pulling AuthService.BuildResult — same shape, minus the streak/badge fluff.
	private static CollaboratorDto MapCollaborator(Domain.Models.Entities.Collaborator c) => new()
	{
		Id = c.Id,
		FullName = c.FullName,
		Email = c.Email,
		PhotoUrl = c.PhotoUrl,
		Status = c.Status,
		DepartmentId = c.DepartmentId,
		DepartmentName = c.Department?.Name,
		RoleId = c.RoleId,
		RoleTitle = c.Role?.Title,
		ManagerId = c.ManagerId,
		ManagerName = c.Manager?.FullName,
		TotalXp = c.TotalXp,
		CurrentLevel = c.CurrentLevel,
		CurrentStreakDays = c.CurrentStreakDays,
		LongestStreakDays = c.LongestStreakDays,
		BadgeCount = c.Badges?.Count ?? 0,
		RoleKind = c.RoleKind,
		EmailVerified = c.EmailVerified,
		JoinDate = c.JoinDate,
	};
}
