namespace Waao.Services.Tenancy;

/// <summary>
/// Stable, hardcoded tenant IDs for the seeded tenants. Used by:
///  - the initial migration's data backfill
///  - SaveChangesInterceptor as the fallback when ITenantContext is empty (legacy JWTs)
///  - tests that need a deterministic tenant
///
/// DO NOT change these once shipped — they are written into the database.
/// </summary>
public static class TenantConstants
{
	/// <summary>The original tenant; every pre-multi-tenant row gets backfilled to this id.</summary>
	public static readonly Guid WaaoTenantId = new("00000000-0000-0000-0000-00000000A0A0");

	/// <summary>The second tenant — created by the same seed migration.</summary>
	public static readonly Guid LibertyTenantId = new("00000000-0000-0000-0000-00000000B0B0");
}
