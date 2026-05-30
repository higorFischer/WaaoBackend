using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.TimeOff;

public class TimeOffRequest : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public string CollaboratorName { get; set; } = string.Empty;

	public TimeOffType Type { get; set; }
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public string? Reason { get; set; }

	public TimeOffStatus Status { get; set; } = TimeOffStatus.Pending;

	public Guid? ReviewedById { get; set; }
	public string? ReviewerName { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public string? ReviewNote { get; set; }
}
