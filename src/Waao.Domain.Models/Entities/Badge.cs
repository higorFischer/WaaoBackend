using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities;

public class Badge : Entity
{
	public string Code { get; set; } = string.Empty;       // e.g., TENURE_1_YEAR (empty for manual badges)
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string IconEmoji { get; set; } = "🏅";
	public string? IconUrl { get; set; }
	public BadgeCategory Category { get; set; }
	public BadgeRarity Rarity { get; set; } = BadgeRarity.Common;
	public int XpReward { get; set; }
	public string? UnlockRule { get; set; }                // human-readable rule description
	public bool IsHidden { get; set; }                     // hidden until earned
	public bool IsManual { get; set; }                     // true = admin-created flair badge, never auto-awarded
	public string? ColorHex { get; set; }                  // optional accent color, e.g. "#FF5733"

	public virtual ICollection<CollaboratorBadge> Holders { get; set; } = [];
}
