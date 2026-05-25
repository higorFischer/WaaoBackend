using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Messaging;

public class MessageAttachment : Entity
{
	public Guid MessageId { get; set; }
	public virtual Message Message { get; set; } = null!;

	public MessageAttachmentKind Kind { get; set; }
	public string Url { get; set; } = string.Empty;
	public string Mime { get; set; } = string.Empty;
	public string OriginalName { get; set; } = string.Empty;
	public long SizeBytes { get; set; }

	/// <summary>Set for audio attachments only (seconds).</summary>
	public int? DurationSeconds { get; set; }
}
