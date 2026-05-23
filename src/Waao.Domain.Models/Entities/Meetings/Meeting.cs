using Waao.Domain.Models.Entities.Calendar;

namespace Waao.Domain.Models.Entities.Meetings;

public class Meeting : Entity
{
	public Guid CalendarEventId { get; set; }
	public virtual CalendarEvent CalendarEvent { get; set; } = null!;

	public Guid OrganizerId { get; set; }
	public virtual Collaborator Organizer { get; set; } = null!;

	public bool TranscriptionEnabled { get; set; } = false;

	/// <summary>Opaque URL-safe token used to issue guest video tokens without authentication.</summary>
	public string GuestToken { get; set; } = string.Empty;

	public virtual ICollection<MeetingAttendee> Attendees { get; set; } = [];
	public virtual ICollection<MeetingAgendaItem> AgendaItems { get; set; } = [];
}
