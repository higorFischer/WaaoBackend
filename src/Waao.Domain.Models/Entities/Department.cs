namespace Waao.Domain.Models.Entities;

public class Department : Entity
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#6366F1";

	/// <summary>Parent department for hierarchy (e.g. Engineering -> Backend / Frontend / Mobile).</summary>
	public Guid? ParentDepartmentId { get; set; }
	public virtual Department? ParentDepartment { get; set; }
	public virtual ICollection<Department> Children { get; set; } = [];

	public virtual ICollection<Collaborator> Collaborators { get; set; } = [];
}
