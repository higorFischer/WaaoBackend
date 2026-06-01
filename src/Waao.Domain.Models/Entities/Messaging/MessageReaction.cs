namespace Waao.Domain.Models.Entities.Messaging;

/// <summary>
/// One emoji reaction by one collaborator on one message. Same person can have
/// multiple DIFFERENT emojis on a message but never the same emoji twice — a
/// composite unique index on (MessageId, CollaboratorId, Emoji) enforces it.
/// Stored as plain UTF-8 (e.g. "👍"); we treat them as opaque strings server-side.
/// </summary>
public class MessageReaction : Entity
{
	public Guid MessageId { get; set; }
	public virtual Message Message { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public string Emoji { get; set; } = string.Empty;
}
