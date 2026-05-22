using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Notifications;

public class Notification : Entity
{
	public Guid RecipientId { get; set; }
	public virtual Collaborator Recipient { get; set; } = null!;

	public NotificationKind Kind { get; set; }

	public string Title { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;

	/// <summary>"channel" or "meeting"</summary>
	public string LinkType { get; set; } = string.Empty;
	public Guid LinkId { get; set; }

	public Guid? ActorId { get; set; }
	public virtual Collaborator? Actor { get; set; }

	public bool IsRead { get; set; }
	public DateTime? ReadAt { get; set; }
}
