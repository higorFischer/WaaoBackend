using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Feedback;

public class PeerFeedback : Entity
{
	public Guid GiverId { get; set; }
	public virtual Collaborator Giver { get; set; } = null!;
	/// <summary>Snapshot — convenient for joins and for staff moderation.</summary>
	public string GiverName { get; set; } = string.Empty;

	public Guid RecipientId { get; set; }
	public virtual Collaborator Recipient { get; set; } = null!;
	public string RecipientName { get; set; } = string.Empty;

	public PeerFeedbackCategory Category { get; set; } = PeerFeedbackCategory.Positive;
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// When true, the recipient does NOT see the giver. Staff can still see the giver
	/// for moderation (the field is never deleted at rest — only filtered on read).
	/// </summary>
	public bool IsAnonymous { get; set; }

	public bool Acknowledged { get; set; }
	public DateTime? AcknowledgedAt { get; set; }
}
