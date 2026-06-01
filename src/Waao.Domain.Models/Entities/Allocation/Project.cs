namespace Waao.Domain.Models.Entities.Allocation;

public class Project : Entity
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string ColorHex { get; set; } = "#2A6B7E";
	public int Position { get; set; }
	public bool IsArchived { get; set; }

	public double PositionX { get; set; }
	public double PositionY { get; set; }

	public Guid? ParentProjectId { get; set; }
	public virtual Project? Parent { get; set; }
	public virtual ICollection<Project> Children { get; set; } = [];

	/// <summary>Optional owning department (drives filters on the dashboard / allocation board).</summary>
	public Guid? DepartmentId { get; set; }
	public virtual Department? Department { get; set; }

	public virtual ICollection<ProjectAllocation> Allocations { get; set; } = [];
	public virtual ICollection<ProjectConnection> OutgoingConnections { get; set; } = [];
}

public class ProjectAllocation : Entity
{
	public Guid ProjectId { get; set; }
	public virtual Project Project { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public string? Note { get; set; }
	public int Position { get; set; }
	public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
	public Guid? AllocatedById { get; set; }
}
