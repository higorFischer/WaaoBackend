namespace Waao.Domain.Models.Entities.Calls;

public class CallChannel : Entity
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#2A6B7E";
	public int Position { get; set; }
	public bool IsArchived { get; set; }

	public Guid CreatedById { get; set; }
	public string CreatedByName { get; set; } = string.Empty;
}
