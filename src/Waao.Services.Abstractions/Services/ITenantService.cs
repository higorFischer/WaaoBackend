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
}
