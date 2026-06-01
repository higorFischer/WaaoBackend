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

public record CreateTenantDto
{
	public string Name { get; init; } = string.Empty;
	public string Slug { get; init; } = string.Empty;
	public string AccentColorHex { get; init; } = "#2A6B7E";
	public string? LogoUrl { get; init; }
}

public record UpdateTenantDto
{
	public string Name { get; init; } = string.Empty;
	public string Slug { get; init; } = string.Empty;
	public string AccentColorHex { get; init; } = "#2A6B7E";
	public string? LogoUrl { get; init; }
	public bool IsActive { get; init; } = true;
}

public record TenantAllowedDomainDto
{
	public Guid Id { get; init; }
	public string Domain { get; init; } = string.Empty;
}

public record AddAllowedDomainDto
{
	public string Domain { get; init; } = string.Empty;
}
