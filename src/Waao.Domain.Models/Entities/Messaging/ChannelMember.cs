namespace Waao.Domain.Models.Entities.Messaging;

public class ChannelMember : Entity
{
	public Guid ChannelId { get; set; }
	public virtual Channel Channel { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public Guid? LastReadMessageId { get; set; }
	public virtual Message? LastReadMessage { get; set; }

	public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
