namespace Waao.Domain.Models.Entities;

/// <summary>
/// One email domain (e.g. "waao.com.br") a tenant has allowlisted. Used at
/// registration time to:
///   1. Route the new collaborator to the right tenant — first tenant whose
///      allowed list contains the email's domain wins.
///   2. Skip the email verification round-trip — because the tenant has
///      already vouched for everyone at that domain.
///
/// Stored lowercased and trimmed; unique per tenant via a partial index.
/// NOT a TenantScopedEntity because we read across tenants at registration
/// time (no JWT yet), but the row carries a real TenantId FK.
/// </summary>
public class TenantAllowedEmailDomain : Entity
{
	public Guid TenantId { get; set; }
	public virtual Tenant Tenant { get; set; } = null!;

	public string Domain { get; set; } = string.Empty;
}
