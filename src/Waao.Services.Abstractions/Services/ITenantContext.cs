namespace Waao.Services.Abstractions.Services;

/// <summary>
/// Resolves the current tenant for a request — populated from the JWT 'tenant_id' claim,
/// or the seeded default tenant for pre-multi-tenant tokens that don't carry it.
/// </summary>
public interface ITenantContext
{
	/// <summary>The tenant the current request operates within. Null when unresolved (rare).</summary>
	Guid? CurrentTenantId { get; }

	/// <summary>Throws if the tenant is not set — call from places that absolutely require it.</summary>
	Guid RequireTenantId();

	/// <summary>Set explicitly (e.g. background services that loop tenants).</summary>
	void SetTenant(Guid tenantId);
}
