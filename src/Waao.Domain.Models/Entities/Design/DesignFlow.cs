using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Design;

public class DesignFlow : Entity
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DesignFlowStatus Status { get; set; } = DesignFlowStatus.Active;

	public virtual ICollection<DesignStep> Steps { get; set; } = [];
	public virtual ICollection<DesignStepEdge> Edges { get; set; } = [];
}
