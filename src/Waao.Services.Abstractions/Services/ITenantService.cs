using Waao.Services.Abstractions.Dtos;

namespace Waao.Services.Abstractions.Services;

public interface ITenantService
{
	/// <summary>Every tenant the given email has an account in. Used by the login picker.</summary>
	Task<IReadOnlyList<TenantDto>> ListForEmailAsync(string email, CancellationToken ct = default);

	/// <summary>Every active tenant — for super-admin views.</summary>
	Task<IReadOnlyList<TenantDto>> ListAllAsync(CancellationToken ct = default);

	/// <summary>Tenant the caller is currently in.</summary>
	Task<TenantDto?> GetCurrentAsync(CancellationToken ct = default);

	/// <summary>
	/// Re-issues a JWT for the caller acting inside a different tenant.
	/// Requires a matching Collaborator row with the same email in the target tenant.
	/// </summary>
	Task<AuthResultDto> SwitchAsync(Guid currentCollaboratorId, Guid targetTenantId, CancellationToken ct = default);

	/// <summary>
	/// Creates a new tenant and seeds it with a mirror of the calling admin as its
	/// first member (Admin role kind). After this returns, the admin can call
	/// <see cref="SwitchAsync"/> to step into the new tenant.
	/// </summary>
	Task<TenantDto> CreateAsync(Guid creatorCollaboratorId, CreateTenantDto dto, CancellationToken ct = default);

	/// <summary>Updates display fields on an existing tenant. Slug edits revalidate uniqueness.</summary>
	Task<TenantDto> UpdateAsync(Guid tenantId, UpdateTenantDto dto, CancellationToken ct = default);

	/// <summary>
	/// Mirrors the calling admin into an existing tenant they don't yet belong to. No-op if a
	/// mirror already exists. Used to bootstrap empty tenants (e.g. seeded Liberty) so an admin
	/// can step into them via the sidebar switcher.
	/// </summary>
	Task<AuthResultDto> JoinAsync(Guid currentCollaboratorId, Guid targetTenantId, CancellationToken ct = default);
}
