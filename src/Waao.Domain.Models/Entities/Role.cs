namespace Waao.Domain.Models.Entities;

public class Role : Entity
{
	public string Title { get; set; } = string.Empty;
	public string? Track { get; set; }                // e.g., Engineering, Product, People
	public int SeniorityOrder { get; set; }           // Jr=1, Mid=2, Sr=3, Staff=4, Principal=5
	public string? Description { get; set; }

	public virtual ICollection<Collaborator> Collaborators { get; set; } = [];
}
