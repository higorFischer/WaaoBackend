namespace Waao.Domain.Models.Entities;

/// <summary>
/// A multi-tenant boundary. Every other entity carries a TenantId (shadow property)
/// pointing here. The "WAAO" tenant is seeded by the first migration to preserve all
/// pre-existing data; new tenants like "Liberty" are added via the tenants admin.
/// </summary>
public class Tenant : Entity
{
	public string Name { get; set; } = string.Empty;
	/// <summary>URL-safe identifier — used for subdomain routing or tenant pickers.</summary>
	public string Slug { get; set; } = string.Empty;
	public string? LogoUrl { get; set; }
	public string AccentColorHex { get; set; } = "#2A6B7E";
	public bool IsActive { get; set; } = true;
}
