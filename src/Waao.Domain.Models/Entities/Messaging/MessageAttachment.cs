using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Messaging;

public class MessageAttachment : Entity
{
	public Guid MessageId { get; set; }
	public virtual Message Message { get; set; } = null!;

	public MessageAttachmentKind Kind { get; set; }

	/// <summary>Public URL for legacy/public attachments. Empty for private ones (served via presigned URL).</summary>
	public string Url { get; set; } = string.Empty;

	/// <summary>Private R2 object key. When set, the attachment lives in the private bucket and is served
	/// via a short-lived presigned URL re-generated on each read. Null = legacy public attachment.</summary>
	public string? StorageKey { get; set; }

	public string Mime { get; set; } = string.Empty;
	public string OriginalName { get; set; } = string.Empty;
	public long SizeBytes { get; set; }

	/// <summary>Set for audio attachments only (seconds).</summary>
	public int? DurationSeconds { get; set; }
}
