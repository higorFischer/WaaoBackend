using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Waao.Domain.Models.Entities;
using Waao.Services.Abstractions.Services;

namespace Waao.Services.Tenancy;

/// <summary>
/// Stamps the current tenant on every entity being inserted that has a
/// 'TenantId' property (real or shadow) and where the caller hasn't already
/// set one. Idempotent: never overwrites an explicit value.
///
/// Source of truth for the tenant is <see cref="ITenantContext"/>. When the
/// context is empty (e.g. background services that don't loop tenants),
/// inserts default to the seeded WAAO tenant to keep the data safe.
/// </summary>
public sealed class TenantSaveChangesInterceptor(ITenantContext TenantContext) : SaveChangesInterceptor
{
	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		Stamp(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		Stamp(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private void Stamp(DbContext? db)
	{
		if (db is null) return;
		var fallback = TenantContext.CurrentTenantId ?? TenantConstants.WaaoTenantId;

		foreach (var entry in db.ChangeTracker.Entries())
		{
			if (entry.State != EntityState.Added) continue;
			if (entry.Entity is Tenant) continue; // tenants table has no tenant_id

			var tenantProp = entry.Metadata.FindProperty("TenantId");
			if (tenantProp is null) continue;

			var current = entry.Property("TenantId").CurrentValue;
			if (current is null)
				entry.Property("TenantId").CurrentValue = fallback;
		}
	}
}
