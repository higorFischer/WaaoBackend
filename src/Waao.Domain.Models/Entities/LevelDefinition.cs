namespace Waao.Domain.Models.Entities;

public class LevelDefinition : Entity
{
	public int Level { get; set; }
	public long XpThreshold { get; set; }       // minimum total XP to reach this level
	public string Title { get; set; } = string.Empty;    // e.g., "Rookie", "Contributor", "Specialist"
	public string IconEmoji { get; set; } = "⭐";
	public string ColorHex { get; set; } = "#6366F1";
}
