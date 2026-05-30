namespace Waao.Services.Abstractions.Dtos.Badges;

public record CreateBadgeDefinitionDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string IconEmoji { get; init; } = "🏅";
	public string? ColorHex { get; init; }
}

public record UpdateBadgeDefinitionDto
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public string IconEmoji { get; init; } = "🏅";
	public string? ColorHex { get; init; }
}

public record GrantBadgeDto
{
	public Guid CollaboratorId { get; init; }
	public Guid BadgeId { get; init; }
	public DateTime? ExpiresAt { get; init; }
	public string? Note { get; init; }
}

public record FlairBadgeDto
{
	public Guid Id { get; init; }          // CollaboratorBadge grant id
	public Guid BadgeId { get; init; }
	public string Name { get; init; } = string.Empty;
	public string IconEmoji { get; init; } = "🏅";
	public string? ColorHex { get; init; }
	public DateTime AwardedAt { get; init; }
	public DateTime? ExpiresAt { get; init; }
	public string? Note { get; init; }     // Context field
}

public record CollaboratorFlairDto
{
	public Guid CollaboratorId { get; init; }
	public IReadOnlyList<FlairBadgeDto> Badges { get; init; } = [];
}
