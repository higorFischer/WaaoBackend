namespace Waao.Domain.Models.Entities.Allocation;

public class ProjectConnection : Entity
{
	public Guid SourceProjectId { get; set; }
	public virtual Project SourceProject { get; set; } = null!;

	public Guid TargetProjectId { get; set; }
	public virtual Project TargetProject { get; set; } = null!;

	public string? Label { get; set; }
}
