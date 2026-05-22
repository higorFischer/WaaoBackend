using Waao.Domain.Models.Entities.Calendar;

namespace Waao.Domain.Models.Entities.Meetings;

public class Meeting : Entity
{
	public Guid CalendarEventId { get; set; }
	public virtual CalendarEvent CalendarEvent { get; set; } = null!;

	public Guid OrganizerId { get; set; }
	public virtual Collaborator Organizer { get; set; } = null!;

	public virtual ICollection<MeetingAttendee> Attendees { get; set; } = [];
	public virtual ICollection<MeetingAgendaItem> AgendaItems { get; set; } = [];
}
