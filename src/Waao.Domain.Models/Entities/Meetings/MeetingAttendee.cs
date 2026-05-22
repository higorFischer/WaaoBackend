using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Meetings;

public class MeetingAttendee : Entity
{
	public Guid MeetingId { get; set; }
	public virtual Meeting Meeting { get; set; } = null!;

	public Guid CollaboratorId { get; set; }
	public virtual Collaborator Collaborator { get; set; } = null!;

	public MeetingRsvp Rsvp { get; set; } = MeetingRsvp.NoResponse;
	public DateTime? RespondedAt { get; set; }

	/// <summary>Set when the attendee was added through a department invite; null when invited individually.</summary>
	public Guid? InvitedViaDepartmentId { get; set; }
	public virtual Department? InvitedViaDepartment { get; set; }
}
