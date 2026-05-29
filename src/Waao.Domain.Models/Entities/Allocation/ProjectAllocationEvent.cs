using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Allocation;

public class ProjectAllocationEvent : Entity
{
	public Guid CollaboratorId { get; set; }
	public Guid ProjectId { get; set; }
	public string ProjectTitle { get; set; } = string.Empty;
	public AllocationEventType EventType { get; set; }
	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
	public Guid? ActorId { get; set; }
}
