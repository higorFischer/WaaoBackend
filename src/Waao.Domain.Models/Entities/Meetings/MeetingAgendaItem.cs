namespace Waao.Domain.Models.Entities.Meetings;

public class MeetingAgendaItem : Entity
{
	public Guid MeetingId { get; set; }
	public virtual Meeting Meeting { get; set; } = null!;

	public int Order { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Notes { get; set; }
	public int? DurationMinutes { get; set; }
}
