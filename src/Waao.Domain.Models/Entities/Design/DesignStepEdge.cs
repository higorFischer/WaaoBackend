namespace Waao.Domain.Models.Entities.Design;

public class DesignStepEdge : Entity
{
	public Guid FlowId { get; set; }
	public virtual DesignFlow Flow { get; set; } = null!;

	public Guid SourceStepId { get; set; }
	public virtual DesignStep SourceStep { get; set; } = null!;

	public Guid TargetStepId { get; set; }
	public virtual DesignStep TargetStep { get; set; } = null!;
}
