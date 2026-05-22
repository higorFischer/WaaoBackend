namespace Waao.Domain.Models.Entities.Messaging;

public class MessageMention : Entity
{
	public Guid MessageId { get; set; }
	public virtual Message Message { get; set; } = null!;

	public Guid MentionedCollaboratorId { get; set; }
	public virtual Collaborator MentionedCollaborator { get; set; } = null!;
}
