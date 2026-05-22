using Waao.Domain.Models.Enums;

namespace Waao.Domain.Models.Entities.Calendar;

public class Calendar : Entity
{
	public string Name { get; set; } = string.Empty;
	public string ColorHex { get; set; } = "#2A6B7E";
	public CalendarScope Scope { get; set; } = CalendarScope.Personal;

	/// <summary>Set for Personal calendars — the owning collaborator.</summary>
	public Guid? OwnerId { get; set; }
	public virtual Collaborator? Owner { get; set; }

	/// <summary>Set for Department calendars — the associated department.</summary>
	public Guid? DepartmentId { get; set; }
	public virtual Department? Department { get; set; }

	public virtual ICollection<CalendarEvent> Events { get; set; } = [];
}
