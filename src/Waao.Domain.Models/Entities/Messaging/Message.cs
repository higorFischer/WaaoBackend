namespace Waao.Domain.Models.Entities.Messaging;

public class Message : Entity
{
	public Guid ChannelId { get; set; }
	public virtual Channel Channel { get; set; } = null!;

	public Guid AuthorId { get; set; }
	public virtual Collaborator Author { get; set; } = null!;

	public string Body { get; set; } = string.Empty;
}
