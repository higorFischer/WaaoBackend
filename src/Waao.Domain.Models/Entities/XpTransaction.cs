using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities;

public class XpTransaction : Entity
{
	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public int Amount { get; set; }
	public XpSource Source { get; set; }
	public string Reason { get; set; } = string.Empty;

	public Guid? SourceEntityId { get; set; }          // e.g., CareerEvent.Id or Badge.Id
	public string? SourceEntityType { get; set; }      // "CareerEvent" | "Badge" | "Streak" | ...

	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
