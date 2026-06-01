using Waao.Services.Abstractions.Services;

namespace Waao.Services.Tenancy;

/// <summary>
/// Scoped per request. Populated by middleware that reads the 'tenant_id' JWT claim;
/// background services explicitly call <see cref="SetTenant"/> per tenant loop iteration.
/// </summary>
public sealed class TenantContext : ITenantContext
{
	public Guid? CurrentTenantId { get; private set; }

	public Guid RequireTenantId()
		=> CurrentTenantId ?? throw new InvalidOperationException("No tenant resolved for the current scope.");

	public void SetTenant(Guid tenantId) => CurrentTenantId = tenantId;
}
