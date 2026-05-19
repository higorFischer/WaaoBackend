namespace Waao.Domain.Models.Entities;

public class Department : Entity
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#6366F1";

	public virtual ICollection<Collaborator> Collaborators { get; set; } = [];
}
