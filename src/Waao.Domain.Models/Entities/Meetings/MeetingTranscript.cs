namespace Waao.Domain.Models.Entities.Meetings;

public class MeetingTranscript : Entity
{
	public Guid MeetingId { get; set; }
	public virtual Meeting Meeting { get; set; } = null!;

	public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

	public virtual ICollection<MeetingTranscriptLine> Lines { get; set; } = [];
}
