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
