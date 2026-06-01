namespace Waao.Services.Abstractions.Dtos;

public record TenantDto
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string Slug { get; init; } = string.Empty;
	public string? LogoUrl { get; init; }
	public string AccentColorHex { get; init; } = "#2A6B7E";
	public bool IsActive { get; init; }
}

public record SwitchTenantDto
{
	public Guid TenantId { get; init; }
}
