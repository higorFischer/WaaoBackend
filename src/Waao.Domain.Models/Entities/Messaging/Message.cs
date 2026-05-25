namespace Waao.Domain.Models.Entities.Messaging;

public class Message : Entity
{
	public Guid ChannelId { get; set; }
	public virtual Channel Channel { get; set; } = null!;

	public Guid AuthorId { get; set; }
	public virtual Collaborator Author { get; set; } = null!;

	public string Body { get; set; } = string.Empty;

	/// <summary>Optional parent message — used for reply threads. Null = top-level message.</summary>
	public Guid? ParentMessageId { get; set; }
	public virtual Message? ParentMessage { get; set; }

	/// <summary>Set the first time the author edits the message; updated on each subsequent edit.</summary>
	public DateTime? EditedAtUtc { get; set; }
}
