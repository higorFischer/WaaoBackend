using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Design;

public class DesignStep : Entity
{
	public Guid FlowId { get; set; }
	public virtual DesignFlow Flow { get; set; } = null!;

	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DesignStepStatus Status { get; set; } = DesignStepStatus.NotStarted;

	public double PositionX { get; set; }
	public double PositionY { get; set; }

	public virtual ICollection<DesignAsset> Assets { get; set; } = [];
}
